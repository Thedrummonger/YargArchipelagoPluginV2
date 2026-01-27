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
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public static class GUIStyles
    {
        private static GUIStyle _opaqueWindow;

        public static GUIStyle OpaqueWindow()
        {
            if (_opaqueWindow == null)
            {
                _opaqueWindow = new GUIStyle(GUI.skin.window);
                Texture2D bgTexture = new Texture2D(1, 1);
                bgTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1f)); // Darker grey
                bgTexture.Apply();
                _opaqueWindow.normal.background = bgTexture;
                _opaqueWindow.onNormal.background = bgTexture;
                _opaqueWindow.hover.background = bgTexture;
                _opaqueWindow.onHover.background = bgTexture;
                _opaqueWindow.normal.textColor = Color.white;
                _opaqueWindow.hover.textColor = Color.white;
                _opaqueWindow.onNormal.textColor = Color.white;
                _opaqueWindow.onHover.textColor = Color.white;
            }
            return _opaqueWindow;
        }
    }

    public class ArchipelagoConnectionDialog : MonoBehaviour
    {
        public static ArchipelagoConnectionDialog Instance { get; private set; }

        [Header("State")]
        public bool Show = false;

        [Header("Defaults")]

        private bool showDeathlinkDropdown = false;
        APConnectionContainer connectionContainer;

        ConnectionDetails connectionDetails;


        private Rect _windowRect = new Rect(20, 20, 400, 180);

        public void Initialize(APConnectionContainer container)
        {
            connectionContainer = container;
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

            connectionDetails = new ConnectionDetails();
        }

        private void OnGUI()
        {
            if (!Show) return;
            _windowRect = GUI.Window(0xA1C4, _windowRect, DrawWindow, "Archipelago Connection", GUIStyles.OpaqueWindow());
        }
        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.Address = GUILayout.TextField(connectionDetails.Address, GUILayout.Width(280));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot Name", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.SlotName = GUILayout.TextField(connectionDetails.SlotName, GUILayout.Width(280));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Password", GUILayout.Width(80));
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.Password = GUILayout.PasswordField(connectionDetails.Password, '*', GUILayout.Width(280));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            string buttonText = connectionContainer.IsSessionConnected ? "Disconnect" : "Connect";
            if (GUILayout.Button(buttonText, GUILayout.Height(28)))
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                if (connectionContainer.IsSessionConnected)
                {
                    connectionContainer.Disconnect();
                    ToastManager.ToastInformation($"Disconnected from AP");
                }
                else
                {
                    ToastManager.ToastInformation($"Connecting to {connectionDetails.SlotName}@{connectionDetails.Address}");
                    connectionContainer.Connect(connectionDetails);
                }
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Close", GUILayout.Height(28)))
                Show = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
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

            var FilteredObjects = FilterItems(Objects, CurrentFilter, GetDisplay);
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


        private static readonly Dictionary<(Type, string), object> _filterCache = new Dictionary<(Type, string), object>();
        public static T[] FilterItems<T>(IEnumerable<T> Objects, string filterText, Func<T, string> GetDisplay)
        {
            var cacheKey = (typeof(T), filterText);
            if (_filterCache.TryGetValue(cacheKey, out var cachedResult))
                return (T[])cachedResult;

            var result = Objects
                .Where(s => GetDisplay(s).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(s => GetDisplay(s), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _filterCache[cacheKey] = result;

            return result;
        }

        public static SongAPData[] GetAvailableSongs(APConnectionContainer container)
        {
            var Valid = container.SlotData.Songs.Where(x =>
                x.HasAvailableLocations(container) &&
                x.IsSongUnlocked(container)
            ).ToHashSet();
            return Valid.ToArray();
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
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, WindowTitle, GUIStyles.OpaqueWindow());
        }

        protected abstract void DrawWindow(int id);

        protected virtual void Initialize(APConnectionContainer container, Rect size, bool Center = true)
        {
            BlockerDialog = YargEngineActions.ShowBlockerDialog();
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
        private SongAPData SelectedSong;

        private string CurrentFilter = "";
        private int currentPage = 0;
        protected override string WindowTitle => "Lower Difficulty";
        public static void ShowMenu(APConnectionContainer container, StaticYargAPItem item)
        {
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
                (currentPage, CurrentFilter) = FormHelpers.DisplayItemList(FormHelpers.GetAvailableSongs(container), 5, currentPage, "SELECT SONG TO LOWER DIFFICULTY", CurrentFilter, GetDisplay, OnSongSelect);
            else
            {
                GUILayout.Label("SELECT A DIFFICULTY VALUE TO LOWER", GUI.skin.label);
                GUILayout.Space(10);
                GUILayout.Label(GetDisplay(SelectedSong), GUI.skin.label);
                GUILayout.Space(10);
                var CurrentReqs = SelectedSong.GetCurrentCompletionRequirements(container);
                if (CurrentReqs.Reward1Diff > SupportedDifficulty.Easy)
                    if (GUILayout.Button($"Lower Reward 1 Difficulty: {CurrentReqs.Reward1Diff.GetDescription()} -> {(CurrentReqs.Reward1Diff - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.Reward1Diff - 1, CurrentReqs.Reward1Req, CurrentReqs.Reward2Diff, CurrentReqs.Reward2Req);
                if (CurrentReqs.Reward1Req > CompletionReq.Clear)
                    if (GUILayout.Button($"Lower Reward 1 Score Requirement: {CurrentReqs.Reward1Req.GetDescription()} -> {(CurrentReqs.Reward1Req - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.Reward1Diff, CurrentReqs.Reward1Req - 1, CurrentReqs.Reward2Diff, CurrentReqs.Reward2Req);
                if (CurrentReqs.Reward2Diff > SupportedDifficulty.Easy)
                    if (GUILayout.Button($"Lower Reward 2 Difficulty: {CurrentReqs.Reward2Diff.GetDescription()} -> {(CurrentReqs.Reward2Diff - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.Reward1Diff, CurrentReqs.Reward1Req, CurrentReqs.Reward2Diff - 1, CurrentReqs.Reward2Req);
                if (CurrentReqs.Reward2Req > CompletionReq.Clear)
                    if (GUILayout.Button($"Lower Reward 2 Score Requirement: {CurrentReqs.Reward2Req.GetDescription()} -> {(CurrentReqs.Reward2Req - 1).GetDescription()}", GUILayout.Height(40)))
                        SetRequirementOverride(CurrentReqs.Reward1Diff, CurrentReqs.Reward1Req, CurrentReqs.Reward2Diff, CurrentReqs.Reward2Req - 1);
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
                Reward1Diff = reward1Diff,
                Reward2Diff = reward2Diff,
                Reward1Req = reward1Req,
                Reward2Req = reward2Req
            };
            container.seedConfig.AdjustedDifficulties[SelectedSong.UniqueKey] = NewReqs;
            container.seedConfig.ApItemsUsed.Add(_item);
            container.seedConfig.Save();
            RemoveBlockerDialog();
            var SongData = SelectedSong.GetYargSongEntry(container);
            var Display = SongData is null ? SelectedSong.GetActiveHash(container) : $"{SongData.Name} by {SongData.Artist}";
            YargEngineActions.ShowPoolData(container, $"New Requirements for {Display}", new SongPool { Instrument = SelectedSong.GetPool(container.SlotData).Instrument, CompletionRequirements = NewReqs });
            CloseMenu();
        }

        private void OnSongSelect(SongAPData data)
        {
            var CurrentReqs = data.GetCurrentCompletionRequirements(container);
            var CanLower1Diff = CurrentReqs.Reward1Diff > SupportedDifficulty.Easy;
            var CanLower2Diff = CurrentReqs.Reward2Diff > SupportedDifficulty.Easy;
            var CanLower1Req = CurrentReqs.Reward1Req > CompletionReq.Clear;
            var CanLower2Req = CurrentReqs.Reward2Req > CompletionReq.Clear;
            if (!CanLower1Diff && !CanLower2Diff && !CanLower1Req && !CanLower2Req)
            {
                ToastManager.ToastError($"Unable to lower the requirements of this song any further!");
                return;
            }
            SelectedSong = data;
        }

        private string GetDisplay(SongAPData songAPData) => songAPData.GetDisplayName(container, true);
    }

    public class SwapSongMenu : BlockerMenu<SwapSongMenu>
    {
        private StaticYargAPItem _item;
        private SongAPData selectedSongToReplace;

        private string CurrentFilter = "";
        private int currentPage = 0;

        // I think this calculates every frame while the window is up so we should cache it.
        Dictionary<SongAPData, SongEntry[]> ValidEntryCache = new Dictionary<SongAPData, SongEntry[]>();

        protected override string WindowTitle => "Swap Song";

        public static void ShowMenu(APConnectionContainer container, StaticYargAPItem item)
        {
            if (CurrentInstance != null)
                return;

            var menu = CreateMenu();
            menu.Initialize(container, new Rect(50, 50, 500, 400));
            menu._item = item;
            menu.ValidEntryCache = new Dictionary<SongAPData, SongEntry[]>();
            menu.Show = true;
        }

        protected override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            if (selectedSongToReplace is null)
                (currentPage, CurrentFilter) = FormHelpers.DisplayItemList(FormHelpers.GetAvailableSongs(container), 5, currentPage, "SELECT SONG TO REPLACE", CurrentFilter, GetDisplay, OnSongToReplaceSelected);
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

        private void OnSongToReplaceSelected(SongAPData song)
        {
            var validReplacements = GetValidReplacements(song);

            if (validReplacements.Length == 0)
            {
                ToastManager.ToastError("There are no valid replacement songs available for this selection.");
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

        private SongEntry[] GetValidReplacements(SongAPData song)
        {
            if (ValidEntryCache.ContainsKey(song))
                return ValidEntryCache[song];

            var Pool = song.GetPool(container.SlotData);
            var UsedSongs = new HashSet<string>();
            foreach (var item in container.SlotData.SongsByInstrument[Pool.Instrument])
            {
                //Add both the original hash and proxy hash if it exists. Even if it's proxied, we probably
                //shouldn't place a core song onto another song as a proxy. This might change in the future.
                UsedSongs.Add(item.Hash);
                if (item.HasProxy(container, out var proxyHash))
                    UsedSongs.Add(proxyHash);

            }
            var AllYargSongs =  YargEngineActions.GetYargSongExportData(SongContainer.Instruments);
            var ValidReplcements = AllYargSongs.Where(x => x.Value.TryGetDifficulty(Pool.Instrument, out var difficulty) && DifficultyInRange(difficulty) && !UsedSongs.Contains(x.Key));
            ValidEntryCache[song] = ValidReplcements.Select(x => x.Value.YargSongEntry).ToArray();
            return ValidReplcements.Select(x => x.Value.YargSongEntry).ToArray();

            bool DifficultyInRange(int difficulty)
            {
                // Let the user manually pick a song outside of their diffuclty range if they want
                if (_item.Type == StaticItems.SwapPick) 
                    return true;
                return difficulty <= Pool.MaxDifficulty && difficulty >= Pool.MinDifficulty;
            }
        }

        private string GetDisplay(SongEntry songEntry) => $"{songEntry.Name} by {songEntry.Artist}";

        private string GetDisplay(SongAPData songAPData) => songAPData.GetDisplayName(container, true);

        private void PerformSwap(SongAPData toReplace, SongEntry replacement)
        {
            string ToReplace = toReplace.GetDisplayName(container, false);
            string Replacement = $"{replacement.Name} by {replacement.Artist}";
            container.seedConfig.SongProxies[toReplace.UniqueKey] = Convert.ToBase64String(replacement.Hash.HashBytes);
            container.seedConfig.ApItemsUsed.Add(_item);
            container.seedConfig.Save();
            RemoveBlockerDialog();
            YargEngineActions.UpdateRecommendedSongsMenu();
            DialogManager.Instance.ShowMessage($"Song Replaced", $"Replaced\n{ToReplace}\n\nwith\n{Replacement}\n\nIn Pool\n{toReplace.PoolName}");
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
            if (GUILayout.Button($"Swap Song (Random) {ExtraAPFunctionalityHelper.FormatLargeNumber(ExtraAPFunctionalityHelper.PriceDict[StaticItems.LowerDifficulty])}", GUILayout.Height(40)))
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
                ToastManager.ToastError($"Not enough energy to purchase a {Item.GetDescription()}!");
                return;
            }
            ToastManager.ToastSuccess($"Purchased one {Item.GetDescription()}");
        }
    }
}
