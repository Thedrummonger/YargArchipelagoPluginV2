using System;
using System.Collections.Generic;
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
    public class SwapSongMenu : MonoBehaviour
    {
        public MessageDialog BlockerDialog;
        public static SwapSongMenu CurrentInstance = null;
        private APConnectionContainer container;
        private StaticYargAPItem item;

        private SongAPData selectedSongToReplace;

        private string filterText = "";
        private int currentPage = 0;
        private const int SONGS_PER_PAGE = 5;

        private Rect windowRect = new Rect(50, 50, 500, 400);

        public bool Show { get; set; }

        public void Initialize(APConnectionContainer container, StaticYargAPItem item)
        {
            BlockerDialog = YargEngineActions.ShowBlockerDialog();
            this.container = container;
            this.item = item;
            selectedSongToReplace = null;
            filterText = "";
            currentPage = 0;
            CurrentInstance = this;
            windowRect = new Rect((Screen.width - 500) / 2, (Screen.height - 400) / 2, 500, 400);
        }

        private void OnGUI()
        {
            if (!Show) return;
            windowRect = GUI.Window(0xA1C5, windowRect, DrawWindow, "Swap Song", GUIStyles.OpaqueWindow());
        }

        private void DrawWindow(int id)
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

            var displayList = GetFilteredList();
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(displayList.Count / (float)SONGS_PER_PAGE));
            currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

            var pageSongs = displayList.Skip(currentPage * SONGS_PER_PAGE).Take(SONGS_PER_PAGE);

            if (selectedSongToReplace != null)
            {
                foreach (var song in pageSongs)
                    if (GUILayout.Button(GetDisplay(song), GUILayout.Height(40)))
                        OnReplacementSelected(song);
            }
            else
            {
                foreach (var song in pageSongs)
                    if (GUILayout.Button(GetDisplay(song), GUILayout.Height(40)))
                        OnSongToReplaceSelected(song);
            }

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

            GUILayout.Space(10);

            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                Show = false;
                Destroy(gameObject);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private List<object> GetFilteredList()
        {
            if (selectedSongToReplace != null)
            {
                var replacements = GetValidReplacements(selectedSongToReplace);
                return replacements
                    .Where(s => GetDisplay(s).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(s => GetDisplay(s), StringComparer.OrdinalIgnoreCase)
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                var songsToReplace = GetSongsToReplace();
                return songsToReplace
                    .Where(s => GetDisplay(s).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(s => GetDisplay(s), StringComparer.OrdinalIgnoreCase)
                    .Cast<object>()
                    .ToList();
            }
        }

        private void OnSongToReplaceSelected(object song)
        {
            selectedSongToReplace = (SongAPData)song;

            var validReplacements = GetValidReplacements(selectedSongToReplace);

            if (validReplacements.Count == 0)
            {
                ToastManager.ToastError("There are no valid replacement songs available for this selection.");
                selectedSongToReplace = null;
                return;
            }

            if (item.Type == StaticItems.SwapRandom)
            {
                var randomReplacement = validReplacements[UnityEngine.Random.Range(0, validReplacements.Count)];
                OnReplacementSelected(randomReplacement);
                return;
            }

            filterText = "";
            currentPage = 0;
        }

        private void OnReplacementSelected(object song)
        {
            var replacement = (SongEntry)song;
            PerformSwap(selectedSongToReplace, replacement);
            ExitMenu();
        }

        public void ExitMenu()
        {
            Show = false;
            Destroy(gameObject);
        }

        private List<SongAPData> GetSongsToReplace()
        {
            var Valid = container.SlotData.LocationIDtoAPData.Values.Where(x =>
                x.HasAvailableLocations(container) &&
                x.IsSongUnlocked(container)
            ).ToHashSet();
            return Valid.ToList();
        }

        private List<SongEntry> GetValidReplacements(SongAPData song)
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
            return ValidReplcements.ToList();
        }

        private string GetDisplay(object song)
        {
            if (song is SongAPData songAPData)
            {
                SongEntry YArgEntry = SongContainer.Songs.FirstOrDefault(x => Convert.ToBase64String(x.Hash.HashBytes) == songAPData.Hash);
                if (YArgEntry is null)
                    return $"[{songAPData.PoolName}] {songAPData.Hash}";
                return $"[{songAPData.PoolName}] {YArgEntry.Name} by {YArgEntry.Artist}";
            }
            if (song is SongEntry songEntry)
                return $"{songEntry.Name} by {songEntry.Artist}";
            throw new NotImplementedException($"Cannot Get name for object of type {song.GetType()}");
        }

        private void PerformSwap(SongAPData toReplace, SongEntry replacement)
        {
            SongEntry YArgEntry = SongContainer.Songs.FirstOrDefault(x => Convert.ToBase64String(x.Hash.HashBytes) == toReplace.Hash);
            string ToReplace = YArgEntry is null ? $"{toReplace.Hash}" : $"{YArgEntry.Name} by {YArgEntry.Artist}";
            string Replacement = $"{replacement.Name} by {replacement.Artist}";
            toReplace.ProxyHash = Convert.ToBase64String(replacement.Hash.HashBytes);
            container.seedConfig.ApItemsUsed.Add(item);
            container.seedConfig.Save(container);
            RemoveBlockerDialog();
            YargEngineActions.UpdateRecommendedSongsMenu();
            DialogManager.Instance.ShowMessage($"Song Replaced", $"Replaced\n{ToReplace}\n\nwith\n{Replacement}\n\nIn Pool\n{toReplace.PoolName}");
        }

        public void RemoveBlockerDialog()
        {
            if (BlockerDialog != null && DialogManager.Instance.IsDialogShowing)
            {
                BlockerDialog = null;
                DialogManager.Instance.ClearDialog();
            }
        }

        private void OnDestroy()
        {
            RemoveBlockerDialog();
        }
    }
}
