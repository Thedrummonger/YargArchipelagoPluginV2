using HarmonyLib;
using System.Reflection;
using UnityEngine.InputSystem;
using YARG.Core.Utility;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;
using YargArchipelagoCommon;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Core;
using YARG.Core.Song;
using YARG.Menu.Dialogs;
using YARG.Menu.Persistent;
using YARG.Song;
using static YargArchipelagoCommon.APWorldData;
using static YargArchipelagoCommon.GUIStyles;

namespace YargArchipelagoCommon
{
    public static class GUIStyles
    {
        public static Vector2 DesignResolution = new(1920f, 1080f);
        private static GUIStyle _opaqueWindow;
        private static Texture2D _bgTexture;
        public static GUIStyle OpaqueWindow()
        {
            if (_opaqueWindow != null)
                return _opaqueWindow;
            _opaqueWindow = new GUIStyle(GUI.skin.window);

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 1f));
            _bgTexture.Apply();
            UnityEngine.Object.DontDestroyOnLoad(_bgTexture);

            _opaqueWindow.normal.background = _bgTexture;
            _opaqueWindow.onNormal.background = _bgTexture;
            _opaqueWindow.hover.background = _bgTexture;
            _opaqueWindow.onHover.background = _bgTexture;

            return _opaqueWindow;
        }

        public static Rect DrawWindowWithScaling(int id, Rect clientRect, GUI.WindowFunction func, string text, GUIStyle style)
        {
            Matrix4x4 originalMatrix = GUI.matrix;

            float scale = Screen.height / DesignResolution.y;
            float scaledWidth = DesignResolution.x * scale;
            float offsetX = (Screen.width - scaledWidth) * 0.5f;

            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, 0, 0), Quaternion.identity, new Vector3(scale, scale, 1f));

            Rect windowRect = GUI.Window(id, clientRect, func, text, style);

            GUI.matrix = originalMatrix;

            return windowRect;
        }
    }

    public class ArchipelagoConnectionDialog : MonoBehaviour
    {
        internal static void UpdateUI()
        {
            if (!Application.isFocused)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            bool DialogModifiersSatisfied =
                (!ArchipelagoPlugin.RequireCtrl.Value || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) &&
                (!ArchipelagoPlugin.RequireShift.Value || kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) &&
                (!ArchipelagoPlugin.RequireAlt.Value || kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            if (DialogModifiersSatisfied && kb[ArchipelagoPlugin.ToggleKey.Value].wasPressedThisFrame)
                Toggle();

            var DevModifiersSatisfied = Keyboard.current != null &&
                Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.shiftKey.isPressed &&
                Keyboard.current.altKey.isPressed;
        }

        public static void Toggle()
        {
            var dialog = GetOrCreateApDialog();
            dialog.Show = !dialog.Show;
        }

        private static ArchipelagoConnectionDialog GetOrCreateApDialog()
        {
            if (ArchipelagoConnectionDialog.Instance != null)
                return ArchipelagoConnectionDialog.Instance;

            var DialogObject = new GameObject("ArchipelagoConnectionDialog");
            DontDestroyOnLoad(DialogObject);
            var dialog = DialogObject.AddComponent<ArchipelagoConnectionDialog>();
            dialog.Initialize(ArchipelagoPlugin.APcontainer);
            return dialog;
        }

        public static void ShowSeedMessage(SeedInfoMessage message) =>
            DialogManager.Instance.ShowMessage(message.Title, message.Body);

        public static void ShowMissingSongs(IEnumerable<string> missing) =>
            DialogManager.Instance.ShowMessage("The following songs were in your AP seed but missing from yarg!",
                APUtils.TruncateString(string.Join(", ", missing), 1000));



        public static ArchipelagoConnectionDialog Instance { get; private set; }

        [Header("State")]
        public bool Show = false;

        [Header("Defaults")]
        APConnectionContainer connectionContainer;

        ConnectionDetails connectionDetails;

        private bool _hasPositioned = false;

        private Rect _windowRect = new Rect(20, 20, 400, 320);

        private static bool ShowChat = false;

        private Vector2 _chatScrollPosition = Vector2.zero;
        private string _chatInputText = "";
        private int _lastChatCount = 0;
        private float _contentHeight = 0;
        GUIStyle richTextStyle = null;

        public void Initialize(APConnectionContainer container)
        {
            connectionContainer = container;
            connectionDetails = ConnectionDetails.Load();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (!Show) return;
            if (!_hasPositioned && Show)
            {
                _windowRect.x = (DesignResolution.x - _windowRect.width) / 2;
                _windowRect.y = (DesignResolution.y - _windowRect.height) / 2;
                _hasPositioned = true;
            }
            _windowRect = GUIStyles.DrawWindowWithScaling(0xA1C4, _windowRect, DrawWindow, "Archipelago Connection", GUIStyles.OpaqueWindow());
        }
        private void DrawWindow(int id)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Event.current.Use();
                if (ShowChat)
                    SendChat();
                else
                    ToggleConnect();
            }
            GUILayout.BeginVertical();

            if (ShowChat)
                ShowChatBox();
            else
                ShowConnectControls();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ShowChat ? "Connection" : "Chat", GUILayout.Height(28)))
                ShowChat = !ShowChat;
            if (GUILayout.Button("Close", GUILayout.Height(28)))
                Show = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void SendChat()
        {
            if (!string.IsNullOrWhiteSpace(_chatInputText))
            {
                connectionContainer.GetSession().Say(_chatInputText);
                _chatInputText = "";
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
            }
        }

        private void ToggleConnect()
        {
            if (connectionContainer.IsConnecting)
                return;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            if (connectionContainer.IsSessionConnected)
            {
                connectionContainer.Disconnect();
            }
            else
            {
                APToastManager.ToastInformation($"Connecting to {connectionDetails.SlotName}@{connectionDetails.Address}");
                connectionContainer.Connect(connectionDetails, UnityEngine.InputSystem.Keyboard.current?.ctrlKey.isPressed ?? false);
            }
        }

        private void ShowChatBox()
        {
            _chatScrollPosition = GUILayout.BeginScrollView(_chatScrollPosition, GUILayout.Height(190));

            if (richTextStyle == null)
            {
                richTextStyle = new GUIStyle(GUI.skin.label);
                richTextStyle.richText = true;
            }

            GUILayout.BeginVertical();
            int startIndex = Mathf.Max(0, ArchipelagoEventManager.ChatHistory.Count - 500);
            for (int i = startIndex; i < ArchipelagoEventManager.ChatHistory.Count; i++)
                GUILayout.Label(ArchipelagoEventManager.ChatHistory[i].ToColoredString(), richTextStyle);
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
                _contentHeight = GUILayoutUtility.GetLastRect().height;

            GUILayout.EndScrollView();

            float maxScroll = Mathf.Max(0, _contentHeight - 190);
            bool isAtBottom = _chatScrollPosition.y >= maxScroll - 10;

            if (ArchipelagoEventManager.ChatHistory.Count > _lastChatCount)
            {
                if (isAtBottom || _lastChatCount == 0)
                    _chatScrollPosition.y = float.MaxValue;

                _lastChatCount = ArchipelagoEventManager.ChatHistory.Count;
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Message", GUILayout.Width(80));
            _chatInputText = GUILayout.TextField(_chatInputText, GUILayout.Width(280));
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            using (new GUIEnabledScope(connectionContainer.IsSessionConnected))
                if (GUILayout.Button("Send", GUILayout.Height(28)))
                    SendChat();
        }

        private void ShowConnectControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
            {
                GUI.SetNextControlName("Address");
                connectionDetails.Address = GUILayout.TextField(connectionDetails.Address, GUILayout.Width(280));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot Name", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
            {
                GUI.SetNextControlName("SlotName");
                connectionDetails.SlotName = GUILayout.TextField(connectionDetails.SlotName, GUILayout.Width(280));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Password", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
            {
                GUI.SetNextControlName("Password");
                connectionDetails.Password = GUILayout.PasswordField(connectionDetails.Password, '*', GUILayout.Width(280));
            }
            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                Event.current.Use();
                string focused = GUI.GetNameOfFocusedControl();

                if (focused == "Address")
                    GUI.FocusControl("SlotName");
                else if (focused == "SlotName")
                    GUI.FocusControl("Password");
                else if (focused == "Password")
                    GUI.FocusControl("Address");
                else
                    GUI.FocusControl("Address");
            }


            GUILayout.Space(10);
            string buttonText = connectionContainer.IsSessionConnected ? "Disconnect" : connectionContainer.IsConnecting ? "Connecting..." : "Connect";
            using (new GUIEnabledScope(!connectionContainer.IsConnecting))
                if (GUILayout.Button(buttonText, GUILayout.Height(28)))
                    ToggleConnect();

            GUILayout.Space(6);

            bool isConnected = connectionContainer.IsSessionConnected &&
                               connectionContainer.SlotData != null &&
                               connectionContainer.seedConfig != null;

            using (new GUIEnabledScope(isConnected))
            {
                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(GUILayout.Width(124));
                string deathLinkYaml = isConnected ? connectionContainer.SlotData.DeathLink.GetDescription() : "N/A";
                GUILayout.Label($"Death Link:");
                GUILayout.Label($"YAML: {deathLinkYaml}");
                string deathLinkText = isConnected ? connectionContainer.seedConfig.DeathLinkMode.GetDescription() : "N/A";
                if (GUILayout.Button(deathLinkText, GUILayout.Height(20)))
                    if (isConnected)
                    {
                        connectionContainer.seedConfig.DeathLinkMode = EnumDescriptions.Next(connectionContainer.seedConfig.DeathLinkMode);
                        connectionContainer.seedConfig.Save();
                    }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(124));
                GUILayout.Label($"DL Trigger:");
                GUILayout.Label($"YAML: {DeathLinkTriggerType.both.GetDescription()}"); //No YAML setting yet so we'll just hard code for now
                string deathLinkTriggerText = isConnected ? connectionContainer.seedConfig.DeathLinkTrigger.GetDescription() : "N/A";
                if (GUILayout.Button(deathLinkTriggerText, GUILayout.Height(20)))
                    if (isConnected)
                    {
                        connectionContainer.seedConfig.DeathLinkTrigger = EnumDescriptions.Next(connectionContainer.seedConfig.DeathLinkTrigger);
                        connectionContainer.seedConfig.Save();
                    }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(124));
                string energyLinkYaml = isConnected ? connectionContainer.SlotData.EnergyLink.GetDescription() : "N/A";
                GUILayout.Label($"Energy Link:");
                GUILayout.Label($"YAML: {energyLinkYaml}");
                string energyLinkText = isConnected ? connectionContainer.seedConfig.EnergyLinkMode.GetDescription() : "N/A";
                if (GUILayout.Button(energyLinkText, GUILayout.Height(20)))
                    if (isConnected)
                    {
                        connectionContainer.seedConfig.EnergyLinkMode = EnumDescriptions.Next(connectionContainer.seedConfig.EnergyLinkMode);
                        connectionContainer.seedConfig.Save();
                    }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(GUILayout.Width(188));
                GUILayout.Label("Item Log:");
                string itemLogText = isConnected ? connectionContainer.seedConfig.InGameItemLog.GetDescription() : "N/A";
                if (GUILayout.Button(itemLogText, GUILayout.Height(20)))
                    if (isConnected)
                    {
                        connectionContainer.seedConfig.InGameItemLog = EnumDescriptions.Next(connectionContainer.seedConfig.InGameItemLog);
                        connectionContainer.seedConfig.Save();
                    }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(188));
                GUILayout.Label("AP Chat:");
                string apChatText = isConnected ? (connectionContainer.seedConfig.InGameAPChat ? "On" : "Off") : "N/A";
                if (GUILayout.Button(apChatText, GUILayout.Height(20)))
                    if (isConnected)
                    {
                        connectionContainer.seedConfig.InGameAPChat = !connectionContainer.seedConfig.InGameAPChat;
                        connectionContainer.seedConfig.Save();
                    }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool _prev;
            public GUIEnabledScope(bool enabled)
            {
                _prev = GUI.enabled;
                GUI.enabled = enabled;
            }
            public void Dispose() => GUI.enabled = _prev;
        }
    }

    public static class FormHelpers
    {
        public static void ShowGoalConditionStatus(APConnectionContainer container) =>
            ArchipelagoConnectionDialog.ShowSeedMessage(SeedInfo.GetGoalConditionStatus(container));

        public static void ShowMacGuffinStatus(int current, int needed, string name)
        {
            var message = SeedInfo.GetGoalProgress(current, needed, name);
            APToastManager.AddToast(current < needed ? APToastManager.APToastType.Error : APToastManager.APToastType.Success,
                $"{message.Title}\n{message.Body}");
        }

        public static void ShowGoalRecieveMessage(APConnectionContainer container, bool received)
        {
            var message = SeedInfo.GetGoalReceiveMessage(container);
            if (!received) APToastManager.ToastError(message.Body);
            else ArchipelagoConnectionDialog.ShowSeedMessage(message);
        }

        public static void ShowPoolData(APConnectionContainer container, string poolName)
        {
            if (!container.SlotData.Pools.ContainsKey(poolName)) return;
            ArchipelagoConnectionDialog.ShowSeedMessage(SeedInfo.GetPoolInfo(container, poolName));
        }


        public static (int CurrentPage, string CurrentFilter) DisplayItemList<T>(IEnumerable<T> Objects, int DisplayCount, int Page, string Title, string LastFilter, Func<T, string> GetDisplay, Action<T> OnClick)
        {
            GUILayout.Label(Title, GUI.skin.label);

            GUILayout.Space(10);

            var CurrentFilter = LastFilter;
            var SelectedPage = Page;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(50));
            string newFilter = GUILayout.TextField(CurrentFilter);
            if (newFilter != CurrentFilter)
            {
                CurrentFilter = newFilter;
                SelectedPage = 0;
            }
            GUILayout.EndHorizontal();

            var FilteredObjects = APUtils.FilterItems(Objects, CurrentFilter, GetDisplay);
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(FilteredObjects.Count() / (float)DisplayCount));
            var currentPage = Mathf.Clamp(SelectedPage, 0, totalPages - 1);
            var Pages = FilteredObjects.Skip(currentPage * DisplayCount).Take(DisplayCount);

            GUILayout.Space(10);
            foreach (var song in Pages)
                if (GUILayout.Button(GetDisplay(song), GUILayout.Height(40)))
                    OnClick(song);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Previous", GUILayout.Height(30)))
            {
                if (currentPage > 0) currentPage--;
            }

            GUILayout.Label($"Page {currentPage + 1} / {totalPages}", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Next", GUILayout.Height(30)))
            {
                if (currentPage < totalPages - 1) currentPage++;
            }

            GUILayout.EndHorizontal();
            return (currentPage, CurrentFilter);
        }

        /// <summary>
        /// Creates an invisible blocking dialog used to prevent UI interaction while custom BepInEx menus are displayed.
        /// Must be closed manually.
        /// </summary>
        public static MessageDialog ShowBlockerDialog()
        {
            var dialog = DialogManager.Instance.ShowMessage("", "");
            dialog.ClearButtons();

            foreach (var graphic in dialog.GetComponentsInChildren<UnityEngine.Component>())
            {   // Keep the "Tint" image, thats what actually blocks the UI 
                if (graphic.GetType().Name == "Image" && graphic.gameObject.name != "Tint")
                {
                    var enabled = graphic.GetType().GetProperty("enabled");
                    enabled?.SetValue(graphic, false);
                }
            }

            return dialog;
        }
    }

    public abstract class BlockerMenu<T> : MonoBehaviour where T : BlockerMenu<T>
    {
        public MessageDialog BlockerDialog;
        protected APConnectionContainer container;
        public static T CurrentInstance { get; protected set; }
        protected virtual string WindowTitle => typeof(T).Name; // Default to class name
        protected virtual int WindowId => typeof(T).Name.GetHashCode(); // Unique ID per type

        public Rect windowRect = new Rect(0, 0, 0, 0);

        public bool Show { get; set; }
        private void OnGUI()
        {
            if (!Show) return;
            windowRect = GUIStyles.DrawWindowWithScaling(WindowId, windowRect, DrawWindow, WindowTitle, GUIStyles.OpaqueWindow());
        }

        protected abstract void DrawWindow(int id);

        protected virtual void Initialize(APConnectionContainer container, Rect size, bool Center = true)
        {
            APUtils.ClearFilters();
            BlockerDialog = FormHelpers.ShowBlockerDialog();
            this.container = container;
            CurrentInstance = (T)this;
            windowRect = size;
            if (Center)
                windowRect = new Rect((Screen.width - windowRect.width) / 2, (Screen.height - windowRect.height) / 2, windowRect.width, windowRect.height);
        }

        protected static T CreateMenu()
        {
            var menuObject = new GameObject(typeof(T).Name);
            var menu = menuObject.AddComponent<T>();
            UnityEngine.Object.DontDestroyOnLoad(menuObject);
            return menu;
        }

        protected void RemoveBlockerDialog()
        {
            if (BlockerDialog != null && DialogManager.Instance.IsDialogShowing)
            {
                BlockerDialog = null;
                DialogManager.Instance.ClearDialog();
            }
        }

        protected virtual void OnDestroy()
        {
            RemoveBlockerDialog();
            if (CurrentInstance == this)
                CurrentInstance = null;
        }

        public void CloseMenu()
        {
            RemoveBlockerDialog();
            Show = false;
            Destroy(gameObject);
        }
    }

    public class LowerDifficultyMenu : BlockerMenu<LowerDifficultyMenu>
    {
        private StaticYargAPItem _item;
        private BaseAPSong SelectedSong;

        private string CurrentFilter = "";
        private int currentPage = 0;
        protected override string WindowTitle => "Lower Difficulty";
        public static void ShowMenu(APConnectionContainer container, StaticYargAPItem item)
        {
            if (FillerItems.GetEditableSongs(container).Length == 0)
            {
                APToastManager.ToastError($"No Available Songs!");
                return;
            }
            if (CurrentInstance != null)
                return;

            var menu = CreateMenu();
            menu.Initialize(container, new Rect(50, 50, 500, 400));
            menu._item = item;
            menu.Show = true;
        }
        protected override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            if (SelectedSong is null)
                (currentPage, CurrentFilter) = FormHelpers.DisplayItemList(FillerItems.GetEditableSongs(container), 5, currentPage, "SELECT SONG TO LOWER DIFFICULTY", CurrentFilter, GetDisplay, OnSongSelect);
            else
            {
                GUILayout.Label("SELECT A DIFFICULTY VALUE TO LOWER", GUI.skin.label);
                GUILayout.Space(10);
                GUILayout.Label(GetDisplay(SelectedSong), GUI.skin.label);
                GUILayout.Space(10);
                var CurrentReqs = SelectedSong.GetCurrentCompletionRequirements(container);
                CompletionReq PotentialNewReq1 = FillerItems.LowerScoreRequirement(CurrentReqs.reward1_req);
                CompletionReq PotentialNewReq2 = FillerItems.LowerScoreRequirement(CurrentReqs.reward2_req);
                if (CurrentReqs.reward1_diff > SupportedDifficulty.Easy)
                    if (GUILayout.Button($"Lower Reward 1 Difficulty: {CurrentReqs.reward1_diff.GetDescription()} -> {(CurrentReqs.reward1_diff - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.reward1_diff - 1, CurrentReqs.reward1_req, CurrentReqs.reward2_diff, CurrentReqs.reward2_req);
                if (CurrentReqs.reward1_req > CompletionReq.Clear)
                    if (GUILayout.Button($"Lower Reward 1 Score Requirement: {CurrentReqs.reward1_req.GetDescription()} -> {(PotentialNewReq1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.reward1_diff, PotentialNewReq1, CurrentReqs.reward2_diff, CurrentReqs.reward2_req);
                if (CurrentReqs.reward2_diff > SupportedDifficulty.Easy)
                    if (GUILayout.Button($"Lower Reward 2 Difficulty: {CurrentReqs.reward2_diff.GetDescription()} -> {(CurrentReqs.reward2_diff - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.reward1_diff, CurrentReqs.reward1_req, CurrentReqs.reward2_diff - 1, CurrentReqs.reward2_req);
                if (CurrentReqs.reward2_req > CompletionReq.Clear)
                    if (GUILayout.Button($"Lower Reward 2 Score Requirement: {CurrentReqs.reward2_req.GetDescription()} -> {(PotentialNewReq2).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.reward1_diff, CurrentReqs.reward1_req, CurrentReqs.reward2_diff, PotentialNewReq2);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                CloseMenu();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

        }

        private void SetRequirementOverride(SupportedDifficulty reward1Diff, CompletionReq reward1Req, SupportedDifficulty reward2Diff, CompletionReq reward2Req)
        {
            var NewReqs = new CompletionRequirements
            {
                reward1_diff = reward1Diff,
                reward2_diff = reward2Diff,
                reward1_req = reward1Req,
                reward2_req = reward2Req
            };
            var message = FillerItems.SetRequirementOverride(container, _item, SelectedSong, NewReqs);
            RemoveBlockerDialog();
            ArchipelagoConnectionDialog.ShowSeedMessage(message);
            CloseMenu();
        }

        private void OnSongSelect(BaseAPSong data)
        {
            var CurrentReqs = data.GetCurrentCompletionRequirements(container);
            if (!FillerItems.CanLowerRequirements(CurrentReqs))
            {
                APToastManager.ToastError($"Unable to lower the requirements of this song any further!");
                return;
            }
            SelectedSong = data;
        }

        private string GetDisplay(BaseAPSong songAPData) => songAPData.GetDisplayName(container, true);
    }

    public class SwapSongMenu : BlockerMenu<SwapSongMenu>
    {
        private StaticYargAPItem _item;
        private BaseAPSong selectedSongToReplace;

        private string CurrentFilter = "";
        private int currentPage = 0;

        // I think this calculates every frame while the window is up so we should cache it.
        Dictionary<BaseAPSong, SongEntry[]> ValidEntryCache = new Dictionary<BaseAPSong, SongEntry[]>();

        protected override string WindowTitle => "Swap Song";

        public static void ShowMenu(APConnectionContainer container, StaticYargAPItem item)
        {
            if (FillerItems.GetEditableSongs(container).Length == 0)
            {
                APToastManager.ToastError($"No Available Songs!");
                return;
            }
            if (CurrentInstance != null)
                return;

            var menu = CreateMenu();
            menu.Initialize(container, new Rect(50, 50, 500, 400));
            menu._item = item;
            menu.ValidEntryCache = new Dictionary<BaseAPSong, SongEntry[]>();
            menu.Show = true;
        }

        protected override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            if (selectedSongToReplace is null)
                (currentPage, CurrentFilter) = FormHelpers.DisplayItemList(FillerItems.GetEditableSongs(container), 5, currentPage, "SELECT SONG TO REPLACE", CurrentFilter, GetDisplay, OnSongToReplaceSelected);
            else
                (currentPage, CurrentFilter) = FormHelpers.DisplayItemList(GetValidReplacements(selectedSongToReplace), 5, currentPage, "SELECT REPLACEMENT", CurrentFilter, GetDisplay, OnReplacementSelected);

            GUILayout.Space(10);

            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                CloseMenu();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void OnSongToReplaceSelected(BaseAPSong song)
        {
            var validReplacements = GetValidReplacements(song);

            if (validReplacements.Length == 0)
            {
                APToastManager.ToastError($"There are no valid replacement songs available for this selection.");
                return;
            }
            selectedSongToReplace = song;

            if (_item.Type == StaticItems.SwapRandom)
            {
                var randomReplacement = validReplacements[UnityEngine.Random.Range(0, validReplacements.Length)];
                OnReplacementSelected(randomReplacement);
                return;
            }

            CurrentFilter = "";
            currentPage = 0;
        }

        private void OnReplacementSelected(SongEntry replacement)
        {
            PerformSwap(selectedSongToReplace, replacement);
            CloseMenu();
        }

        private SongEntry[] GetValidReplacements(BaseAPSong song)
        {
            if (!ValidEntryCache.ContainsKey(song))
                ValidEntryCache[song] = FillerItems.GetValidReplacements(container, song, _item.Type);
            return ValidEntryCache[song];
        }

        private string GetDisplay(SongEntry songEntry) => $"{songEntry.Name} by {songEntry.Artist}";

        private string GetDisplay(BaseAPSong songAPData) => songAPData.GetDisplayName(container, true);

        private void PerformSwap(BaseAPSong toReplace, SongEntry replacement)
        {
            var message = FillerItems.PerformSwap(container, _item, toReplace, replacement);
            RemoveBlockerDialog();
            ArchipelagoConnectionDialog.ShowSeedMessage(message);
        }
    }

    public class EnergyLinkShop : BlockerMenu<EnergyLinkShop>
    {
        protected override string WindowTitle => "ENERGY SHOP";

        public static void ShowMenu(APConnectionContainer container)
        {
            if (CurrentInstance != null)
                return;

            var menu = CreateMenu();
            menu.Initialize(container, new Rect(50, 50, 500, 280));
            menu.Show = true;
        }
        protected override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("SELECT AN ITEM TO PRUCHASE", GUI.skin.label);
            GUILayout.Space(10);
            GUILayout.Label($"CURRENT ENERGY: {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.GetEnergy(container))}", GUI.skin.label);
            GUILayout.Space(10);

            if (GUILayout.Button($"Swap Song (Random) {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.PriceDict[StaticItems.SwapRandom])}", GUILayout.Height(40)))
                PerformPurchase(StaticItems.SwapRandom);
            if (GUILayout.Button($"Swap Song (Pick) {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.PriceDict[StaticItems.SwapPick])}", GUILayout.Height(40)))
                PerformPurchase(StaticItems.SwapPick);
            if (GUILayout.Button($"Lower Difficulty {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.PriceDict[StaticItems.LowerDifficulty])}", GUILayout.Height(40)))
                PerformPurchase(StaticItems.LowerDifficulty);

            GUILayout.Space(10);

            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                CloseMenu();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void PerformPurchase(StaticItems Item)
        {
            var Success = ExtraAPFunctionalityHelper.TryPurchaseItem(container, Item);
            if (!Success)
            {
                APToastManager.ToastError($"Not enough energy to purchase a {Item.GetDescription()}!");
                return;
            }
            APToastManager.ToastSuccess($"Purchased one {Item.GetDescription()}");
            ArchipelagoEventManager.FlagSongLibraryForUpdate();
        }
    }

    public static partial class APToastManager
    {
        private static readonly AccessTools.FieldRef<ToastManager, Toast> ToastPrefabRef =
            AccessTools.FieldRefAccess<ToastManager, Toast>("_toastPrefab");

        private static readonly AccessTools.FieldRef<ToastManager, Color> GeneralColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_generalColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> InformationColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_informationColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> SuccessColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_successColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> WarningColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_warningColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> ErrorColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_errorColor");

        private static readonly Type ToastManagerType = typeof(ToastManager);

        private static readonly Type ToastTypeEnum = ToastManagerType.GetNestedType("ToastType", BindingFlags.NonPublic);

        private static readonly MethodInfo AddToastMethod =
            AccessTools.Method(ToastManagerType, "AddToast", [ToastTypeEnum, typeof(string), typeof(Action)] );

        public static void AddToast(APToastType type, string text, Action onClick = null)
        {
            object enumValue = Enum.ToObject(ToastTypeEnum, (int)type);
            AddToastMethod.Invoke(null, [enumValue, text, onClick]);
        }
        public static bool HandleAPToasts(int type, string body, Action onClick, ToastManager manager)
        {
            if (type < 100) return false;
            var ToastType = (APToastType)type;

            var (text, color, icon) = ToastType switch
            {
                APToastType.General => ("Archipelago", GeneralColorRef(manager), GetIcon(ToastType)),
                APToastType.Information => ("Archipelago", InformationColorRef(manager), GetIcon(ToastType)),
                APToastType.Success => ("Archipelago", SuccessColorRef(manager), GetIcon(ToastType)),
                APToastType.Warning => ("Archipelago", WarningColorRef(manager), GetIcon(ToastType)),
                APToastType.Error => ("Archipelago", ErrorColorRef(manager), GetIcon(ToastType)),
                APToastType.Junk => ("Archipelago", Color.cyan, GetIcon(ToastType)),
                APToastType.Useful => ("Archipelago", Color.slateBlue, GetIcon(ToastType)),
                APToastType.Progression => ("Archipelago", Color.plum, GetIcon(ToastType)),
                APToastType.Trap => ("Archipelago", Color.salmon, GetIcon(ToastType)),
                _ => throw new ArgumentException($"Invalid toast type {type}!")
            };

            var toast = UnityEngine.Object.Instantiate(ToastPrefabRef(manager), manager.transform);
            toast.Initialize(text, body, icon, color, onClick);
            return true;
        }


    }


    public class APSongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry, bool IsHinted, string context = "library") : SongViewType(musicLibrary, songEntry, context)
    {
        public override string GetPrimaryText(bool selected)
        {
            SortString str = SongEntry.Name;
            if (IsHinted)
                return $"* {BaseViewType.FormatAs(str, TextType.Primary, selected)}";
            return BaseViewType.FormatAs(str, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            SortString str = SongEntry.Artist;
            return BaseViewType.FormatAs(str, TextType.Secondary, selected);
        }

        public override Sprite GetIcon()
        {
            SortString str = SongEntry.Source;
            return SongSources.SourceToIcon(str);
        }
    }


    public static partial class APAssets
    {
        private static MethodInfo _loadImage;

        private static void LoadImage(Texture2D texture, byte[] data) =>
            (_loadImage ??= GetLoadImageMI()).Invoke(null, new object[] { texture, data, false });

        static MethodInfo GetLoadImageMI()
        {
            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                 ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine");

            return t.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, [typeof(Texture2D), typeof(byte[]), typeof(bool)], null);
        }
    }

}
