using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
        public static (int CurrentPage, int TotalPages) DisplayItemList<T>(IEnumerable<T> Objects, int DisplayCount, int Page, int Spacing, Func<T, string> GetDisplay, Action<T> OnClick)
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(Objects.Count() / (float)DisplayCount));
            var currentPage = Mathf.Clamp(Page, 0, totalPages - 1);
            var Pages = Objects.Skip(currentPage * DisplayCount).Take(DisplayCount);
            foreach (var song in Pages)
                if (GUILayout.Button(GetDisplay(song), GUILayout.Height(Spacing)))
                    OnClick(song);


            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Previous", GUILayout.Height(Spacing)))
            {
                if (currentPage > 0) currentPage--;
            }

            GUILayout.Label($"Page {currentPage + 1} / {totalPages}", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Next", GUILayout.Height(Spacing)))
            {
                if (currentPage < totalPages - 1) currentPage++;
            }

            GUILayout.EndHorizontal();
            return (currentPage, totalPages);
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

        public static SongAPData[] GetAvailableSongs(APConnectionContainer container, Func<SongAPData, string> GetDisplay, string filterText = "")
        {
            var Valid = container.SlotData.LocationIDtoAPData.Values.Where(x =>
                x.HasAvailableLocations(container) &&
                x.IsSongUnlocked(container)
            ).ToHashSet();
            return FilterItems(Valid, filterText, GetDisplay);
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
        private SongAPData selectedSongToReplace;

        private string filterText = "";
        private int currentPage = 0;
        protected override string WindowTitle => "Lower Difficulty";
        protected override void DrawWindow(int id)
        {

        }
    }

    public class SwapSongMenu : BlockerMenu<SwapSongMenu>
    {
        private StaticYargAPItem _item;
        private SongAPData selectedSongToReplace;

        private string filterText = "";
        private int currentPage = 0;

        protected override string WindowTitle => "Swap Song";

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

            GUILayout.Label(selectedSongToReplace != null ? "SELECT REPLACEMENT" : "SELECT SONG TO REPLACE",
                GUI.skin.label);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(50));
            string newFilter = GUILayout.TextField(filterText);
            if (newFilter != filterText)
            {
                filterText = newFilter;
                currentPage = 0;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            int TotalPages;
            if (selectedSongToReplace is null)
                (currentPage, TotalPages) = FormHelpers.DisplayItemList(FormHelpers.GetAvailableSongs(container, GetDisplay, filterText), 5, currentPage, 40, GetDisplay, OnSongToReplaceSelected);
            else
                (currentPage, TotalPages) = FormHelpers.DisplayItemList(GetValidReplacements(selectedSongToReplace), 5, currentPage, 40, GetDisplay, OnReplacementSelected);

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

            filterText = "";
            currentPage = 0;
        }

        private void OnReplacementSelected(SongEntry replacement)
        {
            PerformSwap(selectedSongToReplace, replacement);
            CloseMenu();
        }

        private SongEntry[] GetValidReplacements(SongAPData song)
        {
            var Pool = song.GetPool(container.SlotData);
            var UsedSongs = new HashSet<string>();
            foreach (var item in container.SlotData.SongPoolToAPData[song.PoolName])
            {
                UsedSongs.Add(item.Key);
                if (item.Value.ProxyHash != null)
                    UsedSongs.Add(item.Value.ProxyHash);

            }
            var ValidReplcements = SongContainer.Songs.Where(x => !UsedSongs.Contains(Convert.ToBase64String(x.Hash.HashBytes)));
            return FormHelpers.FilterItems(ValidReplcements, filterText, GetDisplay);
        }

        private string GetDisplay(SongEntry songEntry) => $"{songEntry.Name} by {songEntry.Artist}";

        private static string GetDisplay(SongAPData songAPData)
        {
            SongEntry YArgEntry = SongContainer.Songs.FirstOrDefault(x => Convert.ToBase64String(x.Hash.HashBytes) == songAPData.Hash);
            if (YArgEntry is null)
                return $"[{songAPData.PoolName}] {songAPData.Hash}";
            return $"[{songAPData.PoolName}] {YArgEntry.Name} by {YArgEntry.Artist}";
        }

        private void PerformSwap(SongAPData toReplace, SongEntry replacement)
        {
            SongEntry YArgEntry = SongContainer.Songs.FirstOrDefault(x => Convert.ToBase64String(x.Hash.HashBytes) == toReplace.Hash);
            string ToReplace = YArgEntry is null ? $"{toReplace.Hash}" : $"{YArgEntry.Name} by {YArgEntry.Artist}";
            string Replacement = $"{replacement.Name} by {replacement.Artist}";
            toReplace.ProxyHash = Convert.ToBase64String(replacement.Hash.HashBytes);
            container.seedConfig.ApItemsUsed.Add(_item);
            container.seedConfig.Save();
            RemoveBlockerDialog();
            YargEngineActions.UpdateRecommendedSongsMenu();
            DialogManager.Instance.ShowMessage($"Song Replaced", $"Replaced\n{ToReplace}\n\nwith\n{Replacement}\n\nIn Pool\n{toReplace.PoolName}");
        }
    }
}
