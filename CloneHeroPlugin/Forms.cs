#nullable enable
using YargArchipelagoCommon;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace YargArchipelagoCommon
{

    public static class ArchipelagoConnectionDialog
    {
        public static List<SeedInfoEntry> GetSeedInfoEntries(APConnectionContainer container)
        {
            var entries = new List<SeedInfoEntry>();
            if (!container.IsSessionConnected || container.seedConfig == null) return entries;
            foreach (var pool in container.SlotData.Pools)
            {
                var name = pool.Key;
                entries.Add(new SeedInfoEntry($"Pool Info: {name.ToUpper()}", () => SeedInfo.GetPoolInfo(container, name)));
            }
            entries.Add(new SeedInfoEntry($"Goal Conditions Met: {container.SlotData.GoalData.IsSongUnlocked(container)}",
                () => SeedInfo.GetGoalConditionStatus(container)));
            AddGoal(StaticItems.SongCompletion, "Setlist", container.SlotData.SetlistNeededForGoal);
            AddGoal(StaticItems.FamePoint, "Fame", container.SlotData.FamePointsForGoal);
            if (container.GoalItemInPool(out var received, out _))
                entries.Add(new SeedInfoEntry($"Goal Item: {(received ? "Found" : "Missing")}", () => SeedInfo.GetGoalReceiveMessage(container)));
            entries.Add(new SeedInfoEntry("Reveal Goal Song", () =>
            {
                var goal = container.SlotData.GoalData;
                return new SeedInfoMessage("GOAL SONG", goal.GetDisplayName(container, true));
            }));
            return entries;

            void AddGoal(StaticItems type, string name, int needed)
            {
                if (needed <= 0) return;
                entries.Add(new SeedInfoEntry($"{name} Goal: {SeedInfo.CountGoalItems(container, type)}/{needed}", () =>
                {
                    var current = SeedInfo.CountGoalItems(container, type);
                    return SeedInfo.GetGoalProgress(current, needed, name);
                }));
            }
        }

        internal static void UpdateUI()
        {
            Tick();
            APToastManager.Drain();
            APToastManager.HandleAPToasts();
        }

        internal static void HandleHotkey()
        {
            if (!IsInitialized || !Application.isFocused || !Input.GetKeyDown(ArchipelagoPlugin.ToggleKey.Value))
                return;
            var satisfied =
                (!ArchipelagoPlugin.RequireCtrl.Value || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                (!ArchipelagoPlugin.RequireShift.Value || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                (!ArchipelagoPlugin.RequireAlt.Value || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            if (satisfied)
                Toggle();
        }

        public static void Toggle()
        {
            Visible = !Visible;
            ArchipelagoPlugin.PluginLog?.LogInfo($"Archipelago connection canvas visible={Visible}");
        }

        public static void ShowMissingSongs(IEnumerable<string> missing) =>
            ShowSeedMessage(new SeedInfoMessage("Missing Songs",
                "The following songs were in your AP seed but missing from Clone Hero!\n\n" + string.Join("\n", missing)));


        private static int _inputBlockedFrame = -1;

        private sealed record ButtonControl(GameObject Object, RectTransform Rect, TMP_Text Text, Image Image);
        private enum DialogPage { Connection, Chat, SeedInfo, SeedDetails, FillerItems, FillerDetails }
    
        private static APConnectionContainer Service => ArchipelagoPlugin.APcontainer;
        private static bool SettingsEnabled => Service.IsSessionConnected && Service.SlotData != null && Service.seedConfig != null;
        private static readonly ConnectionDetails Details = ConnectionDetails.Load();
        private static readonly List<GameObject> ConnectionPage = new();
        private static readonly List<GameObject> ChatPage = new();
        private static GameObject? _root;
        private static TMP_Text? _title;
        private static GameObject? _seedContent;
        private static TMP_Text? _seedBody;
        private static TMP_Text? _seedRange;
        private static ButtonControl? _seedInfo;
        private static ButtonControl? _fillerItems;
        private static ButtonControl? _seedPrevious;
        private static ButtonControl? _seedNext;
        private static readonly List<ButtonControl> SeedRows = new();
        private static List<SeedInfoEntry> _seedEntries = new();
        private static Func<SeedInfoMessage>? _seedDetails;
        private static APSlotData? _seedSlotData;
        private static int _seedOffset;
        private static int _detailPage = 1;
        private static float _nextSeedUpdate;
        private static FillerMenu? _fillerMenu;
        private static List<FillerMenuEntry> _fillerEntries = new();
        private static TMP_Text? _fillerHeading;
        private static TMP_Text? _fillerSearchLabel;
        private static TMP_InputField? _fillerSearch;
        private static string _fillerFilter = "";
        private static int _fillerOffset;
        private const int FillerRowCount = 5;
        private static TMP_Text? _status;
        private static TMP_Text? _chatHistory;
        private static ScrollRect? _chatScroll;
        private static int _lastChatCount = -1;
        private static TMP_InputField? _address;
        private static TMP_InputField? _slotName;
        private static TMP_InputField? _password;
        private static TMP_InputField? _chatInput;
        private static ButtonControl? _connect;
        private static ButtonControl? _page;
        private static ButtonControl? _close;
        private static ButtonControl? _send;
        private static ButtonControl? _deathLink;
        private static ButtonControl? _deathLinkTrigger;
        private static ButtonControl? _energyLink;
        private static TMP_Text? _deathYaml;
        private static TMP_Text? _triggerYaml;
        private static TMP_Text? _energyYaml;
        private static ButtonControl? _itemLog;
        private static ButtonControl? _apChat;
        private static bool _visible;
        internal static bool IsInitialized => _root != null;
        internal static bool BlocksMenuInput => Visible || _inputBlockedFrame == Time.frameCount;
        private static DialogPage _currentPage;
        private static int _lastTickFrame = -1;
    
        internal static bool Visible
        {
            get => _visible;
            set
            {
                if (value && IsSeedPage && (!SettingsEnabled || !ReferenceEquals(_seedSlotData, Service.SlotData)))
                    OpenPage(DialogPage.Connection);
                _visible = value;
                if (_root != null)
                    _root.SetActive(value);
            }
        }
    
        internal static void Initialize(TextMeshProUGUI template)
        {
            if (_root != null)
                return;
    
            var canvasObject = new GameObject("CloneHeroArchipelagoCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
    
            _root = CreateImageObject(canvasObject.transform, "ArchipelagoPanel", Vector2.zero, new Vector2(520, 528));
            var panelImage = _root.GetComponent<Image>()
                             ?? throw new InvalidOperationException("The cloned panel has no Image component");
            panelImage.color = new Color(0.045f, 0.055f, 0.075f, 0.98f);
    
            _title = Label(template, _root.transform, "Archipelago Connection", new Vector2(0, 236), new Vector2(490, 36), 25);
            if (_title == null)
                throw new InvalidOperationException("Could not create the title label");
            _title.alignment = TextAlignmentOptions.Center;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 16;
            _title.fontSizeMax = 25;
    
            CreateConnectionPage(template);
            CreateChatPage(template);
            CreateSeedPage(template);
    
            _page = Button(template, _root.transform, "Chat", new Vector2(-128, -240), new Vector2(240, 34));
            _fillerItems = Button(template, _root.transform, "Filler Items", new Vector2(128, -196), new Vector2(240, 34));
            _seedInfo = Button(template, _root.transform, "Seed Info", new Vector2(-128, -196), new Vector2(240, 34));
            _close = Button(template, _root.transform, "Close", new Vector2(128, -240), new Vector2(240, 34));
            ApplyPageVisibility();
            _root.SetActive(_visible);
            ArchipelagoPlugin.PluginLog?.LogInfo("Created native Archipelago connection canvas");
        }
    
        private static void CreateConnectionPage(TMP_Text template)
        {
            LabelOnPage(template, "Address", new Vector2(-202, 150), new Vector2(95, 30));
            LabelOnPage(template, "Slot Name", new Vector2(-202, 112), new Vector2(95, 30));
            LabelOnPage(template, "Password", new Vector2(-202, 74), new Vector2(95, 30));
            _address = CreateInputField(template, _root!.transform, Details.Address, new Vector2(47, 150), new Vector2(380, 32), false);
            _slotName = CreateInputField(template, _root.transform, Details.SlotName, new Vector2(47, 112), new Vector2(380, 32), false);
            _password = CreateInputField(template, _root.transform, Details.Password, new Vector2(47, 74), new Vector2(380, 32), true);
            ConnectionPage.Add(_address.gameObject);
            ConnectionPage.Add(_slotName.gameObject);
            ConnectionPage.Add(_password.gameObject);
    
            _connect = AddConnectionButton(template, "Connect", new Vector2(0, 29), new Vector2(490, 34));
            _status = LabelOnPage(template, "Disconnected", new Vector2(0, -3), new Vector2(490, 25));
            _status.alignment = TextAlignmentOptions.Center;

            LabelOnPage(template, "Death Link", new Vector2(-170, -40), new Vector2(150, 22), 16);
            LabelOnPage(template, "DL Trigger", new Vector2(0, -40), new Vector2(150, 22), 16);
            LabelOnPage(template, "Energy Link", new Vector2(170, -40), new Vector2(150, 22), 16);
            _deathYaml = LabelOnPage(template, "YAML: N/A", new Vector2(-170, -62), new Vector2(150, 22), 14);
            _triggerYaml = LabelOnPage(template, $"YAML: {DeathLinkTriggerType.both.GetDescription()}", new Vector2(0, -62), new Vector2(150, 22), 14);
            _energyYaml = LabelOnPage(template, "YAML: N/A", new Vector2(170, -62), new Vector2(150, 22), 14);
            _deathLink = AddConnectionButton(template, "N/A", new Vector2(-170, -88), new Vector2(150, 27));
            _deathLinkTrigger = AddConnectionButton(template, "N/A", new Vector2(0, -88), new Vector2(150, 27));
            _energyLink = AddConnectionButton(template, "N/A", new Vector2(170, -88), new Vector2(150, 27));
    
            LabelOnPage(template, "Item Log", new Vector2(-128, -124), new Vector2(235, 22), 16);
            LabelOnPage(template, "AP Chat", new Vector2(128, -124), new Vector2(235, 22), 16);
            _itemLog = AddConnectionButton(template, "N/A", new Vector2(-128, -151), new Vector2(240, 28));
            _apChat = AddConnectionButton(template, "N/A", new Vector2(128, -151), new Vector2(240, 28));
        }
    
        private static void CreateChatPage(TMP_Text template)
        {
            var historyObject = CreateImageObject(_root!.transform, "ChatHistory", new Vector2(0, 47), new Vector2(490, 275));
            var viewport = CreateImageObject(historyObject.transform, "ChatViewport", new Vector2(-7, 0), new Vector2(456, 255));
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            _chatHistory = Label(template, viewport.transform, string.Empty, Vector2.zero, new Vector2(456, 255), 15);
            _chatHistory.richText = true;
            _chatHistory.alignment = TextAlignmentOptions.TopLeft;
            _chatHistory.enableWordWrapping = true;
            _chatHistory.overflowMode = TextOverflowModes.Overflow;
            var content = _chatHistory.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 255);
            content.anchoredPosition = Vector2.zero;
            _chatScroll = historyObject.AddComponent<ScrollRect>();
            _chatScroll.viewport = viewport.GetComponent<RectTransform>();
            _chatScroll.content = content;
            _chatScroll.horizontal = false;
            _chatScroll.vertical = true;
            _chatScroll.movementType = ScrollRect.MovementType.Clamped;
            _chatScroll.inertia = false;
            _chatScroll.scrollSensitivity = 30;
            var track = CreateImageObject(historyObject.transform, "ChatScrollbar", new Vector2(231, 0), new Vector2(10, 255));
            track.GetComponent<Image>().color = DisabledButtonColor;
            var handle = CreateImageObject(track.transform, "Handle", Vector2.zero, Vector2.zero);
            var scrollbar = track.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _chatScroll.verticalScrollbar = scrollbar;
            var historyImage = historyObject.GetComponent<Image>();
            historyImage.color = new Color(0.015f, 0.02f, 0.03f, 0.95f);
            ChatPage.Add(historyObject);
    
            var messageLabel = Label(template, _root.transform, "Message", new Vector2(-208, -113), new Vector2(80, 30), 16);
            ChatPage.Add(messageLabel.gameObject);
            _chatInput = CreateInputField(template, _root.transform, string.Empty, new Vector2(-25, -113), new Vector2(275, 32), false);
            ChatPage.Add(_chatInput.gameObject);
            _send = Button(template, _root.transform, "Send", new Vector2(191, -113), new Vector2(105, 32));
            ChatPage.Add(_send.Object);
        }
    
    
        internal static void Tick()
        {
            if (_lastTickFrame == Time.frameCount)
                return;
            _lastTickFrame = Time.frameCount;
            if (Visible)
                _inputBlockedFrame = Time.frameCount;
    
            HandleHotkey();
            if (Visible)
                _inputBlockedFrame = Time.frameCount;
            if (IsSeedPage && (!SettingsEnabled || !ReferenceEquals(_seedSlotData, Service.SlotData)))
                OpenPage(DialogPage.Connection);
            if (!_visible || _root == null)
                return;
    
            UpdateDisplay();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsFillerPage) BackFromFillerMenu();
                else if (_currentPage == DialogPage.SeedDetails) OpenPage(DialogPage.SeedInfo);
                else if (IsSeedPage) OpenPage(DialogPage.Connection);
                else Visible = false;
                return;
            }
            if (IsSeedPage)
            {
                var scroll = Input.mouseScrollDelta.y;
                if (scroll != 0) ScrollSeedPage(scroll > 0 ? -1 : 1);
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (_currentPage == DialogPage.Chat) SendChat();
                else if (_currentPage == DialogPage.Connection) ToggleConnect();
                return;
            }
            if (_currentPage == DialogPage.Connection && !Service.IsSessionConnected && Input.GetKeyDown(KeyCode.Tab))
            {
                if (_address!.isFocused) _slotName!.ActivateInputField();
                else if (_slotName!.isFocused) _password!.ActivateInputField();
                else _address.ActivateInputField();
                return;
            }
            if (!Input.GetMouseButtonDown(0))
                return;
    
            if (Hit(_close))
            {
                Visible = false;
                return;
            }
            if (Hit(_page))
            {
                OpenPage(_currentPage == DialogPage.Connection ? DialogPage.Chat : DialogPage.Connection);
                return;
            }
            if (Hit(_seedInfo) && SettingsEnabled)
            {
                OpenPage(_currentPage == DialogPage.SeedInfo ? DialogPage.Connection : DialogPage.SeedInfo);
                return;
            }
            if (Hit(_fillerItems) && SettingsEnabled)
            {
                if (IsFillerPage) BackFromFillerMenu();
                else ShowFillerMenu(new FillerItemsMenu(Service));
                return;
            }
            if (IsSeedPage)
            {
                if (!SettingsEnabled) return;
                if (Hit(_seedPrevious)) ScrollSeedPage(-1);
                else if (Hit(_seedNext)) ScrollSeedPage(1);
                else if (_currentPage == DialogPage.SeedInfo)
                    for (var index = 0; index < SeedRows.Count; index++)
                        if (Hit(SeedRows[index]) && _seedOffset + index < _seedEntries.Count)
                        {
                            _seedDetails = _seedEntries[_seedOffset + index].GetDetails;
                            OpenPage(DialogPage.SeedDetails);
                            break;
                        }
                if (_currentPage == DialogPage.FillerItems)
                    for (var index = 0; index < FillerRowCount; index++)
                        if (Hit(SeedRows[index]) && _fillerOffset + index < _fillerEntries.Count)
                        {
                            _fillerEntries[_fillerOffset + index].Select();
                            RefreshSeedDisplay();
                            break;
                        }
                return;
            }
            if (_currentPage == DialogPage.Chat)
            {
                if (Hit(_send))
                    SendChat();
                return;
            }
            if (Hit(_connect))
            {
                ToggleConnect();
                return;
            }
            if (!SettingsEnabled)
                return;
            var settings = Service.seedConfig;
            if (Hit(_deathLink))
            {
                settings.DeathLinkMode = settings.DeathLinkMode.Next();
                Service.UpdateDeathLinkTags();
            }
            else if (Hit(_deathLinkTrigger)) settings.DeathLinkTrigger = settings.DeathLinkTrigger.Next();
            else if (Hit(_energyLink)) settings.EnergyLinkMode = settings.EnergyLinkMode.Next();
            else if (Hit(_itemLog)) settings.InGameItemLog = settings.InGameItemLog.Next();
            else if (Hit(_apChat)) settings.InGameAPChat = !settings.InGameAPChat;
            else return;
            settings.Save();
            UpdateDisplay();
        }

        private static void ToggleConnect()
        {
            if (Service.IsConnecting)
                return;
            if (Service.IsSessionConnected)
                Service.Disconnect();
            else
            {
                Details.Address = _address!.text;
                Details.SlotName = _slotName!.text;
                Details.Password = _password!.text;
                Service.Connect(Details, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            }
        }
    
        private static void UpdateDisplay()
        {
            var connected = Service.IsSessionConnected;
            _status!.text = Service.StatusText;
            _connect!.Text.text = connected ? "Disconnect" : Service.IsConnecting ? "Connecting..." : "Connect";
            _connect.Image.color = Service.IsConnecting ? DisabledButtonColor : ButtonColor;
            _address!.interactable = !connected;
            _slotName!.interactable = !connected;
            _password!.interactable = !connected;
    
            var settings = Service.seedConfig;
            var settingsEnabled = SettingsEnabled;
            foreach (var button in new[] { _seedInfo, _fillerItems })
                SetButtonEnabled(button!, settingsEnabled);
            _deathLink!.Text.text = settingsEnabled ? settings.DeathLinkMode.GetDescription() : "N/A";
            _deathLinkTrigger!.Text.text = settingsEnabled ? settings.DeathLinkTrigger.GetDescription() : "N/A";
            _energyLink!.Text.text = settingsEnabled ? settings.EnergyLinkMode.GetDescription() : "N/A";
            _itemLog!.Text.text = settingsEnabled ? settings.InGameItemLog.GetDescription() : "N/A";
            _apChat!.Text.text = settingsEnabled ? settings.InGameAPChat ? "On" : "Off" : "N/A";
            _deathYaml!.text = settingsEnabled ? $"YAML: {Service.SlotData!.DeathLink.GetDescription()}" : "YAML: N/A";
            _triggerYaml!.text = $"YAML: {DeathLinkTriggerType.both.GetDescription()}";
            _energyYaml!.text = settingsEnabled ? $"YAML: {Service.SlotData!.EnergyLink.GetDescription()}" : "YAML: N/A";
            foreach (var button in new[] { _deathLink, _deathLinkTrigger, _energyLink, _itemLog, _apChat })
            {
                button.Image.color = settingsEnabled ? ButtonColor : DisabledButtonColor;
                button.Text.color = settingsEnabled ? Color.white : Color.gray;
            }
    
    
            if (_currentPage == DialogPage.Chat)
            {
                UpdateChatHistory();
                _send!.Image.color = connected ? ButtonColor : DisabledButtonColor;
            }
            if (IsSeedPage && Time.unscaledTime >= _nextSeedUpdate)
            {
                _nextSeedUpdate = Time.unscaledTime + 0.25f;
                RefreshSeedDisplay();
            }
            if (_currentPage == DialogPage.FillerItems && _fillerSearch!.text != _fillerFilter)
            {
                _fillerFilter = _fillerSearch.text;
                _fillerOffset = 0;
                RefreshSeedDisplay();
            }
        }
    
        private static void UpdateChatHistory()
        {
            if (_lastChatCount == ArchipelagoEventManager.ChatHistory.Count) return;
            var content = _chatHistory!.rectTransform;
            var viewportHeight = _chatScroll!.viewport.rect.height;
            var oldHeight = content.rect.height;
            var position = content.anchoredPosition;
            var atBottom = _lastChatCount < 0 || position.y >= Mathf.Max(0, oldHeight - viewportHeight) - 10;
            _chatHistory.text = string.Join("\n", ArchipelagoEventManager.ChatHistory.Skip(Math.Max(0, ArchipelagoEventManager.ChatHistory.Count - 500)).Select(message => message.ToColoredString()));
            var height = Mathf.Max(viewportHeight, _chatHistory.preferredHeight);
            content.sizeDelta = new Vector2(0, height);
            content.anchoredPosition = new Vector2(0, atBottom ? height - viewportHeight : Mathf.Clamp(position.y, 0, height - viewportHeight));
            _chatScroll.StopMovement();
            _lastChatCount = ArchipelagoEventManager.ChatHistory.Count;
        }

        private static void ApplyPageVisibility()
        {
            foreach (var value in ConnectionPage)
                value.SetActive(_currentPage == DialogPage.Connection);
            foreach (var value in ChatPage)
                value.SetActive(_currentPage == DialogPage.Chat);
            _seedContent!.SetActive(IsSeedPage);
            var expanded = IsSeedPage;
            _root!.GetComponent<RectTransform>().sizeDelta = expanded ? new Vector2(760, 628) : new Vector2(520, 528);
            _title!.rectTransform.anchoredPosition = new Vector2(0, expanded ? 254 : 236);
            _title.rectTransform.sizeDelta = expanded ? new Vector2(720, 96) : new Vector2(490, 36);
            _title.text = _currentPage switch
            {
                DialogPage.Chat => "Archipelago Chat",
                DialogPage.SeedInfo => "Seed Info",
                DialogPage.FillerItems => "Filler Items",
                DialogPage.FillerDetails => _title.text,
                DialogPage.SeedDetails => _title.text,
                _ => "Archipelago Connection"
            };
            if (_page != null)
            {
                _page.Text.text = _currentPage == DialogPage.Connection ? "Chat" : "Connection";
                _page.Rect.anchoredPosition = new Vector2(-128, expanded ? -290 : -240);
                _fillerItems!.Rect.anchoredPosition = new Vector2(128, expanded ? -246 : -196);
                _fillerItems.Text.text = IsFillerPage ? "Back" : "Filler Items";
                _seedInfo!.Rect.anchoredPosition = new Vector2(-128, expanded ? -246 : -196);
                _seedInfo.Text.text = _currentPage is DialogPage.SeedInfo or DialogPage.SeedDetails ? "Back" : "Seed Info";
                _close!.Rect.anchoredPosition = new Vector2(128, expanded ? -290 : -240);
            }
        }

        private static bool IsFillerPage => _currentPage is DialogPage.FillerItems or DialogPage.FillerDetails;
        private static bool IsSeedPage => _currentPage is DialogPage.SeedInfo or DialogPage.SeedDetails || IsFillerPage;

        private static void OpenPage(DialogPage page)
        {
            if ((page is DialogPage.SeedInfo or DialogPage.SeedDetails or DialogPage.FillerItems or DialogPage.FillerDetails) && !SettingsEnabled)
                page = DialogPage.Connection;
            if (page == DialogPage.SeedInfo && _currentPage != DialogPage.SeedDetails) _seedOffset = 0;
            if (page is not (DialogPage.SeedDetails or DialogPage.FillerDetails)) _seedDetails = null;
            if (page is not (DialogPage.FillerItems or DialogPage.FillerDetails)) _fillerMenu = null;
            if (page == DialogPage.FillerItems) _fillerMenu ??= new FillerItemsMenu(Service);
            _fillerSearch?.DeactivateInputField();
            _address?.DeactivateInputField();
            _slotName?.DeactivateInputField();
            _password?.DeactivateInputField();
            _chatInput?.DeactivateInputField();
            _currentPage = page;
            _seedSlotData = IsSeedPage ? Service.SlotData : null;
            _detailPage = 1;
            if (!SettingsEnabled) _seedEntries.Clear();
            ApplyPageVisibility();
            if (IsSeedPage) RefreshSeedDisplay();
        }

        private static void CreateSeedPage(TMP_Text template)
        {
            _seedContent = CreateImageObject(_root!.transform, "SeedContent", Vector2.zero, new Vector2(720, 420));
            _seedContent.GetComponent<Image>().color = Color.clear;
            for (var index = 0; index < 8; index++)
            {
                var row = Button(template, _seedContent.transform, "", new Vector2(0, 180 - index * 48), new Vector2(710, 42));
                row.Text.enableAutoSizing = true;
                row.Text.fontSizeMin = 12;
                row.Text.fontSizeMax = 17;
                row.Text.enableWordWrapping = true;
                SeedRows.Add(row);
            }
            _seedBody = Label(template, _seedContent.transform, "", new Vector2(0, 0), new Vector2(700, 390), 18, "SeedDetails");
            _seedBody.alignment = TextAlignmentOptions.TopLeft;
            _seedBody.enableWordWrapping = true;
            _seedBody.overflowMode = TextOverflowModes.Page;
            _seedPrevious = Button(template, _seedContent.transform, "Up", new Vector2(-268, -208), new Vector2(170, 30));
            _seedNext = Button(template, _seedContent.transform, "Down", new Vector2(268, -208), new Vector2(170, 30));
            _seedRange = Label(template, _seedContent.transform, "", new Vector2(0, -208), new Vector2(330, 30));
            _seedRange.alignment = TextAlignmentOptions.Center;
            _fillerHeading = Label(template, _seedContent.transform, "", new Vector2(0, 172), new Vector2(700, 58), 18);
            _fillerHeading.alignment = TextAlignmentOptions.Center;
            _fillerHeading.enableWordWrapping = true;
            _fillerHeading.enableAutoSizing = true;
            _fillerHeading.fontSizeMin = 12;
            _fillerHeading.fontSizeMax = 18;
            _fillerSearchLabel = Label(template, _seedContent.transform, "Search", new Vector2(-309, 122), new Vector2(90, 32));
            _fillerSearch = CreateInputField(template, _seedContent.transform, "", new Vector2(45, 122), new Vector2(620, 32), false);
        }

        private static void RefreshSeedDisplay()
        {
            if (!SettingsEnabled || !IsSeedPage) return;
            var fillerList = _currentPage == DialogPage.FillerItems;
            _fillerHeading!.gameObject.SetActive(fillerList);
            _fillerSearchLabel!.gameObject.SetActive(fillerList && _fillerMenu!.Searchable);
            _fillerSearch!.gameObject.SetActive(fillerList && _fillerMenu!.Searchable);
            if (fillerList)
            {
                RefreshFillerDisplay();
                return;
            }
            var list = _currentPage == DialogPage.SeedInfo;
            _seedBody!.gameObject.SetActive(!list);
            if (list)
            {
                _seedEntries = GetSeedInfoEntries(Service);
                _seedOffset = Math.Clamp(_seedOffset, 0, Math.Max(0, _seedEntries.Count - SeedRows.Count));
            }
            for (var index = 0; index < SeedRows.Count; index++)
            {
                var visible = list && _seedOffset + index < _seedEntries.Count;
                SeedRows[index].Object.SetActive(visible);
                SeedRows[index].Rect.anchoredPosition = new Vector2(0, 180 - index * 48);
                if (visible) SeedRows[index].Text.text = _seedEntries[_seedOffset + index].Title;
            }
            if (list)
            {
                _seedRange!.text = $"{_seedOffset + 1}-{Math.Min(_seedOffset + SeedRows.Count, _seedEntries.Count)} of {_seedEntries.Count}";
                SetButtonEnabled(_seedPrevious!, _seedOffset > 0);
                SetButtonEnabled(_seedNext!, _seedOffset + SeedRows.Count < _seedEntries.Count);
            }
            else
            {
                var message = _seedDetails!();
                _title!.text = message.Title;
                _seedBody.text = message.Body;
                _seedBody.ForceMeshUpdate();
                _detailPage = Math.Clamp(_detailPage, 1, Math.Max(1, _seedBody.textInfo.pageCount));
                _seedBody.pageToDisplay = _detailPage;
                _seedRange!.text = $"Page {_detailPage} of {Math.Max(1, _seedBody.textInfo.pageCount)}";
                SetButtonEnabled(_seedPrevious!, _detailPage > 1);
                SetButtonEnabled(_seedNext!, _detailPage < _seedBody.textInfo.pageCount);
            }
        }

        private static void ScrollSeedPage(int direction)
        {
            if (!SettingsEnabled) return;
            if (_currentPage == DialogPage.SeedInfo) _seedOffset += direction;
            else if (_currentPage == DialogPage.FillerItems) _fillerOffset += direction;
            else _detailPage += direction;
            RefreshSeedDisplay();
        }

        internal static void ShowFillerMenu(FillerMenu menu)
        {
            _fillerMenu = menu;
            _fillerFilter = "";
            _fillerOffset = 0;
            if (_fillerSearch != null) _fillerSearch.text = "";
            OpenPage(DialogPage.FillerItems);
        }

        internal static void ShowSeedMessage(SeedInfoMessage message)
        {
            _seedDetails = () => message;
            OpenPage(DialogPage.SeedDetails);
            Visible = true;
        }

        internal static void ShowFillerMessage(SeedInfoMessage message)
        {
            _seedDetails = () => message;
            OpenPage(DialogPage.FillerDetails);
        }

        private static void BackFromFillerMenu()
        {
            if (_currentPage == DialogPage.FillerItems && _fillerMenu!.GoBack())
                ShowFillerMenu(_fillerMenu);
            else if (_currentPage == DialogPage.FillerItems && _fillerMenu is FillerItemsMenu)
                OpenPage(DialogPage.Connection);
            else ShowFillerMenu(new FillerItemsMenu(Service));
        }

        private static void RefreshFillerDisplay()
        {
            _seedBody!.gameObject.SetActive(false);
            _fillerEntries = _fillerMenu!.GetEntries();
            if (_fillerMenu.Searchable)
                _fillerEntries = _fillerEntries.Where(entry => entry.Title.Contains(_fillerFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase).ToList();
            _title!.text = _fillerMenu.Title;
            _fillerHeading!.text = _fillerMenu.Heading;
            _fillerOffset = Math.Clamp(_fillerOffset, 0, Math.Max(0, _fillerEntries.Count - FillerRowCount));
            for (var index = 0; index < SeedRows.Count; index++)
            {
                var visible = index < FillerRowCount && _fillerOffset + index < _fillerEntries.Count;
                SeedRows[index].Object.SetActive(visible);
                SeedRows[index].Rect.anchoredPosition = new Vector2(0, (_fillerMenu.Searchable ? 74 : 108) - index * 58);
                if (visible) SeedRows[index].Text.text = _fillerEntries[_fillerOffset + index].Title;
            }
            _seedRange!.text = _fillerEntries.Count == 0 ? "No matching songs" :
                $"{_fillerOffset + 1}-{Math.Min(_fillerOffset + FillerRowCount, _fillerEntries.Count)} of {_fillerEntries.Count}";
            SetButtonEnabled(_seedPrevious!, _fillerOffset > 0);
            SetButtonEnabled(_seedNext!, _fillerOffset + FillerRowCount < _fillerEntries.Count);
        }

        private static void SetButtonEnabled(ButtonControl button, bool enabled)
        {
            button.Image.color = enabled ? ButtonColor : DisabledButtonColor;
            button.Text.color = enabled ? Color.white : Color.gray;
        }
    
        private static void SendChat()
        {
            if (_chatInput == null || !Service.IsSessionConnected || string.IsNullOrWhiteSpace(_chatInput.text))
                return;
            Service.GetSession().Say(_chatInput.text);
            _chatInput.text = string.Empty;
        }
    
        private static TMP_Text LabelOnPage(TMP_Text template, string text, Vector2 position, Vector2 size, float fontSize = 17, string? name = null)
        {
            var label = Label(template, _root!.transform, text, position, size, fontSize, name);
            ConnectionPage.Add(label.gameObject);
            return label;
        }
    
        private static TMP_Text Label(TMP_Text template, Transform parent, string text, Vector2 position, Vector2 size, float fontSize = 17, string? name = null)
        {
            var labelObject = Clone(template, parent, name ?? text, position, size);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }
    
        private static ButtonControl AddConnectionButton(TMP_Text template, string text, Vector2 position, Vector2 size)
        {
            var button = Button(template, _root!.transform, text, position, size);
            ConnectionPage.Add(button.Object);
            return button;
        }
    
        private static ButtonControl Button(TMP_Text template, Transform parent, string text, Vector2 position, Vector2 size)
        {
            var buttonObject = CreateImageObject(parent, text + "Button", position, size);
            var label = Label(template, buttonObject.transform, text, Vector2.zero, size, 17);
            label.text = text;
            label.fontSize = 17;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            return new ButtonControl(buttonObject, buttonObject.GetComponent<RectTransform>(), label, image);
        }
    
        private static TMP_InputField CreateInputField(TMP_Text template, Transform parent, string value, Vector2 position, Vector2 size, bool password)
        {
            var fieldObject = CreateImageObject(parent, "InputField", position, size);
            var image = fieldObject.GetComponent<Image>();
            image.color = new Color(0.01f, 0.015f, 0.025f, 1f);
    
            var textObject = Clone(template, fieldObject.transform, "Text", Vector2.zero, new Vector2(size.x - 18, size.y - 6));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 17;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
    
            var input = fieldObject.AddComponent<TMP_InputField>();
            input.textViewport = fieldObject.GetComponent<RectTransform>();
            input.textComponent = text;
            input.targetGraphic = image;
            input.text = value;
            input.lineType = TMP_InputField.LineType.SingleLine;
            if (password)
                input.contentType = TMP_InputField.ContentType.Password;
            return input;
        }
    
        private static GameObject Clone(TMP_Text template, Transform parent, string name, Vector2 position, Vector2 size)
        {
            var componentTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<TextMeshProUGUI>();
            var clone = new GameObject(name, componentTypes);
            clone.transform.SetParent(parent, false);
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            var text = clone.GetComponent<TextMeshProUGUI>();
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.richText = false;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0;
            text.wordSpacing = 0;
            text.enableAutoSizing = false;
            return clone;
        }
    
        private static GameObject CreateImageObject(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var componentTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<Image>();
            var result = new GameObject(name, componentTypes);
            result.transform.SetParent(parent, false);
            var rect = result.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return result;
        }
    
        private static bool Hit(ButtonControl? button) => button != null && button.Object.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(button.Rect, Input.mousePosition, null);
    
        private static readonly Color ButtonColor = new(0.08f, 0.25f, 0.36f, 1f);
        private static readonly Color DisabledButtonColor = new(0.13f, 0.13f, 0.15f, 1f);
    }

    public sealed record FillerMenuEntry(string Title, Action Select);

    public abstract class FillerMenu
    {
        protected readonly APConnectionContainer container;
        protected FillerMenu(APConnectionContainer container) { this.container = container; }
        public abstract string Title { get; }
        public virtual string Heading => "";
        public virtual bool Searchable => false;
        public abstract List<FillerMenuEntry> GetEntries();
        public virtual bool GoBack() => false;
    }

    public static class FormHelpers
    {
        public static void MarkHintedSongs(APConnectionContainer container, SongSelect menu)
        {
            if (!container.IsSessionConnected) return;
            var titles = CloneHeroMappings.GetSongTitles(menu);
            if (titles == null) return;
            var hints = container.GetSession().Hints.GetHints();
            foreach (var entry in EngineActions.APSections.Values)
            {
                if (!entry.Key.StartsWith("POOL:") || CloneHeroMappings.IsSectionCollapsed(entry.Section)) continue;
                var pool = entry.Key.Substring(5);
                var hintedHashes = new HashSet<string>(container.SlotData.Songs
                    .Where(song => song.PoolName == pool && hints.Any(hint =>
                        hint.LocationId == song.MainLocationID || hint.LocationId == song.ExtraLocationID))
                    .Select(song => song.GetActiveHash(container)), StringComparer.OrdinalIgnoreCase);
                var songs = CloneHeroMappings.GetSectionSongs(entry.Section);
                for (var index = 0; index < songs.Count; index++)
                {
                    var row = CloneHeroMappings.GetSectionStartIndex(entry.Section) + index + 1 - CloneHeroMappings.GetMenuScrollOffset(menu);
                    if (row <= 0 || row >= titles.Length || !hintedHashes.Contains(songs[index].ChecksumString)) continue;
                    var title = titles[row];
                    if (title == null) continue;
                    var text = CloneHeroMappings.GetSongTitleText(title);
                    if (text != null)
                        CloneHeroMappings.SetSongTitle(title, "* " + text.text, row == menu.GetSelectedMenuIndex());
                }
            }
        }

    }

    public sealed class FillerItemsMenu : FillerMenu
    {
        public FillerItemsMenu(APConnectionContainer container) : base(container) { }
        public override string Title => "Filler Items";
        public override string Heading => "SELECT AN ITEM TO USE";

        public override List<FillerMenuEntry> GetEntries()
        {
            var entries = new List<FillerMenuEntry>();
            var items = container.GetAllAquiredActionItems().Where(item => !container.seedConfig.ApItemsUsed.Contains(item)).ToArray();
            foreach (var type in new[] { StaticItems.SwapPick, StaticItems.SwapRandom, StaticItems.LowerDifficulty })
            {
                var available = items.Where(item => item.Type == type).ToArray();
                if (available.Length == 0) continue;
                entries.Add(new FillerMenuEntry($"{type.GetDescription()} ({available.Length} Remaining)", () =>
                {
                    if (FillerItems.GetEditableSongs(container).Length == 0)
                    {
                        APToastManager.Enqueue("Archipelago", "No Available Songs!");
                        return;
                    }
                    ArchipelagoConnectionDialog.ShowFillerMenu(type == StaticItems.LowerDifficulty
                        ? new LowerDifficultyMenu(container, available[0]) : new SwapSongMenu(container, available[0]));
                }));
            }
            entries.Add(new FillerMenuEntry("Open Energy Shop", () => ArchipelagoConnectionDialog.ShowFillerMenu(new EnergyLinkShop(container))));
            return entries;
        }
    }

    public sealed class LowerDifficultyMenu : FillerMenu
    {
        private readonly StaticYargAPItem _item;
        private BaseAPSong? SelectedSong;
        public LowerDifficultyMenu(APConnectionContainer container, StaticYargAPItem item) : base(container) { _item = item; }
        public override string Title => "Lower Difficulty";
        public override bool Searchable => SelectedSong == null;
        public override string Heading => SelectedSong == null ? "SELECT SONG TO LOWER DIFFICULTY" :
            $"SELECT A DIFFICULTY VALUE TO LOWER\n{SelectedSong.GetDisplayName(container, true)}";

        public override List<FillerMenuEntry> GetEntries()
        {
            if (SelectedSong == null)
                return FillerItems.GetEditableSongs(container).Select(song => new FillerMenuEntry(song.GetDisplayName(container, true), () => OnSongSelect(song))).ToList();
            var current = SelectedSong.GetCurrentCompletionRequirements(container);
            var entries = new List<FillerMenuEntry>();
            if (current.reward1_diff > SupportedDifficulty.Easy)
                entries.Add(new FillerMenuEntry($"Lower Reward 1 Difficulty: {current.reward1_diff.GetDescription()} -> {(current.reward1_diff - 1).GetDescription()}", () => SetRequirementOverride(0)));
            if (current.reward1_req > CompletionReq.Clear)
                entries.Add(new FillerMenuEntry($"Lower Reward 1 Score Requirement: {current.reward1_req.GetDescription()} -> {FillerItems.LowerScoreRequirement(current.reward1_req).GetDescription()}", () => SetRequirementOverride(1)));
            if (current.reward2_diff > SupportedDifficulty.Easy)
                entries.Add(new FillerMenuEntry($"Lower Reward 2 Difficulty: {current.reward2_diff.GetDescription()} -> {(current.reward2_diff - 1).GetDescription()}", () => SetRequirementOverride(2)));
            if (current.reward2_req > CompletionReq.Clear)
                entries.Add(new FillerMenuEntry($"Lower Reward 2 Score Requirement: {current.reward2_req.GetDescription()} -> {FillerItems.LowerScoreRequirement(current.reward2_req).GetDescription()}", () => SetRequirementOverride(3)));
            return entries;
        }

        private void OnSongSelect(BaseAPSong song)
        {
            var current = song.GetCurrentCompletionRequirements(container);
            if (!FillerItems.CanLowerRequirements(current))
            {
                APToastManager.Enqueue("Archipelago", "Unable to lower the requirements of this song any further!");
                return;
            }
            SelectedSong = song;
            ArchipelagoConnectionDialog.ShowFillerMenu(this);
        }

        private void SetRequirementOverride(int selection)
        {
            if (SelectedSong == null || !FillerItems.CanUseItem(container, _item, SelectedSong)) return;
            var current = SelectedSong.GetCurrentCompletionRequirements(container);
            var requirements = new CompletionRequirements
            {
                reward1_diff = current.reward1_diff, reward1_req = current.reward1_req,
                reward2_diff = current.reward2_diff, reward2_req = current.reward2_req
            };
            switch (selection)
            {
                case 0 when requirements.reward1_diff > SupportedDifficulty.Easy: requirements.reward1_diff--; break;
                case 1 when requirements.reward1_req > CompletionReq.Clear: requirements.reward1_req = FillerItems.LowerScoreRequirement(requirements.reward1_req); break;
                case 2 when requirements.reward2_diff > SupportedDifficulty.Easy: requirements.reward2_diff--; break;
                case 3 when requirements.reward2_req > CompletionReq.Clear: requirements.reward2_req = FillerItems.LowerScoreRequirement(requirements.reward2_req); break;
                default: return;
            }
            ArchipelagoConnectionDialog.ShowFillerMessage(
                FillerItems.SetRequirementOverride(container, _item, SelectedSong, requirements));
        }

        public override bool GoBack()
        {
            if (SelectedSong == null) return false;
            SelectedSong = null;
            return true;
        }
    }

    public sealed class SwapSongMenu : FillerMenu
    {
        private readonly StaticYargAPItem _item;
        private BaseAPSong? selectedSongToReplace;
        private SongEntry[] validReplacements = Array.Empty<SongEntry>();
        public SwapSongMenu(APConnectionContainer container, StaticYargAPItem item) : base(container) { _item = item; }
        public override string Title => _item.Type.GetDescription();
        public override bool Searchable => true;
        public override string Heading => selectedSongToReplace == null ? "SELECT SONG TO REPLACE" :
            $"SELECT REPLACEMENT\n{selectedSongToReplace.GetDisplayName(container, true)}";

        public override List<FillerMenuEntry> GetEntries() => selectedSongToReplace == null
            ? FillerItems.GetEditableSongs(container).Select(song => new FillerMenuEntry(song.GetDisplayName(container, true), () => OnSongToReplaceSelected(song))).ToList()
            : validReplacements.Select(song => new FillerMenuEntry($"{song.Name_StrippedTags} by {song.Artist_StrippedTags}", () => PerformSwap(song))).ToList();

        private void OnSongToReplaceSelected(BaseAPSong song)
        {
            validReplacements = FillerItems.GetValidReplacements(container, song, _item.Type);
            if (validReplacements.Length == 0)
            {
                APToastManager.Enqueue("Archipelago", "There are no valid replacement songs available for this selection.");
                return;
            }
            selectedSongToReplace = song;
            if (_item.Type == StaticItems.SwapRandom)
                PerformSwap(validReplacements[UnityEngine.Random.Range(0, validReplacements.Length)]);
            else ArchipelagoConnectionDialog.ShowFillerMenu(this);
        }

        private void PerformSwap(SongEntry replacement)
        {
            if (selectedSongToReplace == null || !FillerItems.CanUseItem(container, _item, selectedSongToReplace)) return;
            if (!FillerItems.GetValidReplacements(container, selectedSongToReplace, _item.Type).Any(song => string.Equals(song.ChecksumString, replacement.ChecksumString, StringComparison.OrdinalIgnoreCase)))
            {
                APToastManager.Enqueue("Archipelago", "There are no valid replacement songs available for this selection.");
                return;
            }
            ArchipelagoConnectionDialog.ShowFillerMessage(
                FillerItems.PerformSwap(container, _item, selectedSongToReplace, replacement));
        }

        public override bool GoBack()
        {
            if (selectedSongToReplace == null) return false;
            selectedSongToReplace = null;
            validReplacements = Array.Empty<SongEntry>();
            return true;
        }
    }

    public sealed class EnergyLinkShop : FillerMenu
    {
        private System.Threading.Tasks.Task<long>? energyRequest;
        private string energyDisplay = "Loading...";
        private float nextEnergyUpdate;
        public EnergyLinkShop(APConnectionContainer container) : base(container) { }
        public override string Title => "ENERGY SHOP";
        public override string Heading
        {
            get
            {
                if (energyRequest?.IsCompleted == true)
                {
                    try { energyDisplay = ExtraAPFunctionalityHelper.FormatLargeNumber(energyRequest.GetAwaiter().GetResult()); }
                    catch (Exception e)
                    {
                        energyDisplay = "Unavailable";
                        container.LogWarning?.Invoke($"Could not read Energy Link balance: {e.Message}");
                    }
                    energyRequest = null;
                    nextEnergyUpdate = Time.unscaledTime + 1;
                }
                if (energyRequest == null && Time.unscaledTime >= nextEnergyUpdate)
                    energyRequest = ExtraAPFunctionalityHelper.GetEnergyAsync(container);
                var balance = container.seedConfig.EnergyLinkMode == EnergyLinkType.disabled ? "0" : energyDisplay;
                return $"{(container.IsPurchasingItem ? "PURCHASING..." : "SELECT AN ITEM TO PURCHASE")}\nCURRENT ENERGY: {balance}";
            }
        }
        public override List<FillerMenuEntry> GetEntries() =>
            new[] { StaticItems.SwapRandom, StaticItems.SwapPick, StaticItems.LowerDifficulty }.Select(item =>
                new FillerMenuEntry($"{item.GetDescription()} {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.PriceDict[item])}", () => PerformPurchase(item))).ToList();

        private void PerformPurchase(StaticItems item)
        {
            if (container.IsPurchasingItem || !container.IsSessionConnected) return;
            var settings = container.seedConfig;
            ExtraAPFunctionalityHelper.TryPurchaseItemAsync(container, item, container.MainThreadActions.Enqueue).ContinueWith(purchase => container.MainThreadActions.Enqueue(() =>
            {
                try
                {
                    var purchased = purchase.GetAwaiter().GetResult();
                    if (!container.IsSessionConnected || !ReferenceEquals(settings, container.seedConfig)) return;
                    if (!purchased)
                    {
                        APToastManager.Enqueue("Archipelago", $"Not enough energy to purchase a {item.GetDescription()}!");
                        return;
                    }
                    APToastManager.Enqueue("Archipelago", $"Purchased one {item.GetDescription()}");
                    APPatches.HasAvailableAPSongUpdate = true;
                    nextEnergyUpdate = 0;
                }
                catch (Exception e)
                {
                    container.LogError?.Invoke($"Could not purchase {item.GetDescription()}: {e}");
                    APToastManager.Enqueue("Archipelago", "Could not complete the Energy Link purchase.");
                }
            }));
        }
    }

    public static partial class APToastManager
    {
        private static NotificationPopups? StyledPopup;
        private static Color DefaultOutline;
        private static Sprite? DefaultIcon;
        private static bool DefaultTitleRichText;
        private static bool DefaultMessageRichText;
        private static Sprite? AppliedIcon;
        private static readonly ConcurrentQueue<(string Title, string Message)> Pending = new();

        private static string GetColor(APToastType type) => type switch
        {
            APToastType.Information => "75BFFF",
            APToastType.Success => "00FF00",
            APToastType.Warning => "FFFF00",
            APToastType.Error => "FF0000",
            APToastType.Junk => "00FFFF",
            APToastType.Useful => "6A5ACD",
            APToastType.Progression => "DDA0DD",
            APToastType.Trap => "FA8072",
            _ => "FFFFFF"
        };

        public static void AddToast(APToastType type, string message, Action? onClick = null) =>
            Enqueue($"<color=#{GetColor(type)}>Archipelago</color>", message);


        internal static void HandleAPToasts()
        {
            var popup = CloneHeroMappings.GetNotificationPopup();
            if (popup == null || popup._titleText == null || popup._messageText == null) return;
            var title = popup._titleText.text;
            foreach (var type in Enum.GetValues<APToastType>())
            {
                if (title != $"<color=#{GetColor(type)}>Archipelago</color>" &&
                    !(type == APToastType.General && title == "Archipelago")) continue;
                if (StyledPopup == null || StyledPopup.Pointer != popup.Pointer)
                {
                    StyledPopup = popup;
                    DefaultOutline = popup._outlineImage.color;
                    DefaultIcon = popup._iconImage.sprite;
                    DefaultTitleRichText = popup._titleText.richText;
                    DefaultMessageRichText = popup._messageText.richText;
                }
                popup._titleText.richText = true;
                popup._messageText.richText = true;
                ColorUtility.TryParseHtmlString("#" + GetColor(type), out var color);
                popup._outlineImage.color = color;
                AppliedIcon = GetIcon(type);
                popup._iconImage.sprite = AppliedIcon;
                return;
            }
            if (StyledPopup == null || StyledPopup.Pointer != popup.Pointer) return;
            popup._outlineImage.color = DefaultOutline;
            if (popup._iconImage.sprite == AppliedIcon) popup._iconImage.sprite = DefaultIcon;
            popup._titleText.richText = DefaultTitleRichText;
            popup._messageText.richText = DefaultMessageRichText;
            StyledPopup = null;
        }

        internal static void Enqueue(string title, string message)
        {
            Pending.Enqueue((title, message));
            ArchipelagoPlugin.PluginLog?.LogInfo($"{title}: {message}");
        }

        private static void ShowToast(string title, string body, float duration = 2f) =>
            CloneHeroMappings.ShowToast(
                title, body, ToastType.Info, duration);

        internal static void Drain()
        {
            while (Pending.TryDequeue(out var notification))
            {
                try { ShowToast(notification.Title, notification.Message); }
                catch (Exception e) { ArchipelagoPlugin.PluginLog?.LogWarning($"Could not show notification: {e.Message}"); }
            }
        }
    }


    public static partial class APAssets
    {
        private static void LoadImage(Texture2D texture, byte[] data)
        {
            var loadImage = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")!
                .GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>), typeof(bool) })!;
            loadImage.Invoke(null, new object[] { texture, new Il2CppStructArray<byte>(data), false });
        }
    }


    public sealed record SeedInfoEntry(string Title, Func<SeedInfoMessage> GetDetails);

}
