using static YargArchipelagoCommon.APWorldData;
using YargArchipelagoCommon;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YargArchipelagoCommon
{

    public static class EngineActions
    {
        public static bool IsSupportedInstrument(CloneHeroInstrument source, out SupportedInstrument? target)
        {
            target = source switch
            {
                CloneHeroInstrument.Guitar => SupportedInstrument.FiveFretGuitar,
                CloneHeroInstrument.Bass => SupportedInstrument.FiveFretBass,
                CloneHeroInstrument.Keys => SupportedInstrument.Keys,
                CloneHeroInstrument.Drums => SupportedInstrument.FourLaneDrums,
                CloneHeroInstrument.ProDrums => SupportedInstrument.ProDrums,
                CloneHeroInstrument.Vocals => SupportedInstrument.Vocals,
                _ => null
            };
            return target.HasValue;
        }

        public static SupportedDifficulty GetSupportedDifficulty(CloneHeroDifficulty source)
        {
            if (source > CloneHeroDifficulty.Expert)
                return SupportedDifficulty.Expert;
            if (source < CloneHeroDifficulty.Easy)
                return SupportedDifficulty.Easy;
            return (SupportedDifficulty)((int)source + 1); //SupportedDifficulty starts at 1 to match yarg, clone hero easy is 0.
        }

        public static List<APPlayerResult> GetAPPlayerResults(this GroupSongStats groupStats)
        {
            var stats = CloneHeroMappings.GetStats(groupStats);
            var players = GlobalVariables.instance?.playerList;
            if (stats == null || players == null)
                return null;

            var result = new List<APPlayerResult>();
            for (var index = 0; index < stats.Length && index < players.Length; index++)
            {
                var statsPlayer = stats[index];
                var player = players[index];
                if (statsPlayer == null || player == null || !CloneHeroMappings.IsPlayerActive(player))
                    continue;
                var profile = CloneHeroMappings.GetPlayerProfile(player);
                if (profile == null || CloneHeroMappings.GetDifficulty(profile) == CloneHeroDifficulty.None)
                    continue;

                var stars = CloneHeroMappings.GetStars(statsPlayer);
                result.Add(new APPlayerResult
                {
                    CurrentInstrument = IsSupportedInstrument(CloneHeroMappings.GetInstrument(profile), out var instrument) ? instrument : null,
                    CurrentDifficulty = GetSupportedDifficulty(CloneHeroMappings.GetDifficulty(profile)),
                    Stars = stars,
                    IsSixStars = stars >= 6,
                    IsGoldStars = stars >= 7,
                    IsFc = CloneHeroMappings.IsFullCombo(statsPlayer)
                });
            }
            return result;
        }

        public static string GetSongHash(SongEntry song) => song.ChecksumString;
        public static string GetSongHash(GameManager gameManager) => GetCurrentSong()?.ChecksumString;
        public static string GetSongDisplayName(SongEntry song) => $"{song.Name_StrippedTags} by {song.Artist_StrippedTags}";
        public static StringComparer SongHashComparer => StringComparer.OrdinalIgnoreCase;


        public const int ArchipelagoMenuIndex = 2;
        public const string ArchipelagoMenuKey = "Archipelago";
        public static string SongExportFile => Path.Combine(DataFolder, "CloneHeroSongExport.json");
        private static readonly object SongExportLock = new object();
        private static List<SongEntry> AvailableSongs;
        internal static readonly Dictionary<IntPtr, (string Key, SongSection Section)> APSections = new();
        private static readonly HashSet<string> CollapsedHeaders = new(StringComparer.Ordinal);
        private static string SelectedSection;
        private static string SelectedSong;
        private static bool ReindexingAPSections;
        private static Il2CppSystem.String NativeArchipelagoMenuKey;
        private static RectTransform ArchipelagoMenuRect;
        public static SongExportData GetSongExportData(SongEntry song)
        {
            var Entry = new SongExportData
            {
                Name = song.Name_StrippedTags ?? string.Empty,
                Artist = song.Artist_StrippedTags ?? string.Empty,
                SongChecksum = song.ChecksumString ?? string.Empty,
                Source = !string.IsNullOrWhiteSpace(song.iconName) ? song.iconName : song.topLevelPlaylist ?? string.Empty,
                Album = song.Album_StrippedTags ?? string.Empty,
                Genre = song.Genre_StrippedTags ?? string.Empty,
                Charter = song.Charter_StrippedTags ?? string.Empty,
                Time = Math.Round(song.songLength / 1000d)
            };
            Entry.AddIntensity(SupportedInstrument.FiveFretGuitar, song.GuitarIntensity);
            Entry.AddIntensity(SupportedInstrument.FiveFretBass, song.BassIntensity);
            Entry.AddIntensity(SupportedInstrument.Keys, song.KeysIntensity);
            Entry.AddIntensity(SupportedInstrument.FourLaneDrums, song.DrumsIntensity);
            Entry.AddIntensity(SupportedInstrument.FiveLaneDrums, song.DrumsIntensity);
            Entry.AddIntensity(SupportedInstrument.ProDrums, song.ProDrumsIntensity);
            return Entry;
        }

        public static void DumpAvailableSongs()
        {
            lock (SongExportLock)
            {
                try
                {
                    if (AvailableSongs == null)
                        return;
                    var SongData = GetCloneHeroSongExportData();

                    var TemporaryFile = SongExportFile + ".tmp";
                    SongExportData.WriteToFile(TemporaryFile, SongData);
                    File.Move(TemporaryFile, SongExportFile, true);
                    ArchipelagoPlugin.PluginLog?.LogInfo($"Exported {SongData.Count} Clone Hero songs to {SongExportFile}");
                }
                catch (Exception e)
                {
                    ArchipelagoPlugin.PluginLog?.LogError($"Failed to export Clone Hero songs\n{e}");
                }
            }
        }

        public static List<SongExportData> GetCloneHeroSongExportData()
        {
            var SongData = new List<SongExportData>();
            foreach (var song in GetAvailableSongs())
                SongData.Add(GetSongExportData(song));
            return SongData;
        }

        public static SongEntry GetCurrentSong() => GlobalVariables.instance?.currentSongEntry;

        public static GroupSongStats GetSongResults() => EndOfSong.GroupSongStats;

        public static APSongResult GetSongResults(EndOfSong endScreen)
        {
            var global = GlobalVariables.instance;
            var song = GetCurrentSong();
            var groupStats = GetSongResults();
            if (endScreen == null || song == null)
                return null;
            var players = groupStats.GetAPPlayerResults();
            if (players == null)
                return null;

            return new APSongResult
            {
                Hash = song.ChecksumString,
                Name = $"{song.Name_StrippedTags} by {song.Artist_StrippedTags}",
                IsPractice = global.isPracticeEnabled,
                PlayerHasFailed = global.failed,
                BandScore = CloneHeroMappings.GetBandScore(groupStats),
                Players = players
            };
        }

        public static void FailedSong(APSongResult gameManager)
        {
            var parent = ArchipelagoPlugin.APcontainer;
            if (!parent.IsSessionConnected || parent.seedConfig is null || !parent.seedConfig.SendDlOnSongFail() ||
                gameManager.IsPractice || !gameManager.PlayerHasFailed || APPatches.IgnoreDeathLinkForCurrentSong)
                return;

            APPatches.IgnoreDeathLinkForCurrentSong = true;
            parent.DeathLinkService?.SendDeathLink(new DeathLink(parent.GetSession().Players.ActivePlayer.Name,
                $"Failed Song {gameManager.Name}"));
        }

        public static IEnumerable<BasePlayer> GetPlayers(this GameManager gameManager)
        {
            var players = gameManager == null ? null : gameManager.playerObjects;
            if (players == null)
                yield break;

            foreach (var player in players)
                if (player != null)
                    yield return player;
        }

        public static void ApplyStarPowerItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _)) return;
            foreach (var player in current.GetPlayers())
                if (player.engine != null)
                    CloneHeroMappings.GainStarPower(player.engine, CloneHeroMappings.GetQuarterSpBar(player.engine), false);
        }

        public static void ForceRestartSong(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _) || FadeBehaviour.instance == null) return;
            handler.ResetBuffer();
            CloneHeroMappings.LoadScene(FadeBehaviour.instance, current.gameObject.scene.name);
        }

        public static void ApplyRockMetertrapItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _) || current.isSongOver ||
                current.globalVariables.isPracticeEnabled || current.globalVariables.failed) return;
            foreach (var player in current.GetPlayers())
            {
                if (player.engine == null) continue;
                SetRockMeter(player, CloneHeroMappings.GetRockMeter(player.engine) - 0.25f);
            }
        }

        public static void SetRockMeter(BasePlayer player, float value)
        {
            if (player?.engine == null) return;
            value = Mathf.Clamp01(value);
            CloneHeroMappings.SetRockMeter(player.engine, value);
            CloneHeroMappings.UpdateRockMeter(player, value);
        }

        public static bool TryPreventSongFail(BasePlayer player, float health)
        {
            if (health > 0f || player.engine == null || CloneHeroMappings.IsNoFailEnabled()) return true;
            var handler = ArchipelagoPlugin.APcontainer;
            if (handler == null || !handler.IsSessionConnected || !handler.IsInSong(out var current, out _) ||
                current.isSongOver || current.globalVariables.isPracticeEnabled || current.globalVariables.failed ||
                !current.CouldProductLocationCheck(handler, out _)) return true;

            return !FillerItems.TryUseFailPrevention(handler, () => SetRockMeter(player, 0.25f));
        }

        public static bool ApplyDeathLink(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _) || current.isSongOver ||
                current.globalVariables.isPracticeEnabled || current.globalVariables.failed) return false;
            switch (handler.seedConfig.DeathLinkMode)
            {
                case DeathLinkType.rock_meter:
                    foreach (var player in current.GetPlayers())
                        SetRockMeter(player, 0.02f);
                    break;
                case DeathLinkType.instant_fail:
                    APPatches.IgnoreDeathLinkForCurrentSong = true;
                    current.globalVariables.failed = true;
                    CloneHeroMappings.EndSong(current);
                    break;
                default:
                    return false;
            }
            return true;
        }

        public static List<SongEntry> GetAvailableSongs() =>
            AvailableSongs == null ? new List<SongEntry>() : new List<SongEntry>(AvailableSongs);

        public static void UpdateAvailableSongs()
        {
            var loadedSongs = CloneHeroMappings.GetLoadedSongs();
            if (loadedSongs == null)
                throw new InvalidOperationException("Clone Hero completed its song scan without a song library.");
            var songs = new List<SongEntry>();
            for (var index = 0; index < loadedSongs.Count; index++)
            {
                var song = loadedSongs[index];
                if (song != null)
                    songs.Add(song);
            }
            AvailableSongs = songs;
        }

        public static Dictionary<string, SongEntry> GetSongLookup()
        {
            var songs = new Dictionary<string, SongEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var song in GetAvailableSongs())
                if (!string.IsNullOrWhiteSpace(song.ChecksumString))
                    songs[song.ChecksumString] = song;
            return songs;
        }

        public static SongSection CreateSongSection(string title, IEnumerable<SongEntry> songs)
        {
            var section = new SongSection(title);
            CloneHeroMappings.SetSectionCollapsed(section, false);
            foreach (var song in songs)
                if (song != null)
                    CloneHeroMappings.GetSectionSongs(section).Add(song);
            return section;
        }

        public static void ReindexSongSections(Il2CppSystem.Collections.Generic.List<SongSection> sections)
        {
            if (sections == null)
                return;
            ReindexingAPSections = true;
            try
            {
                CloneHeroMappings.ReindexSongSections(sections);
            }
            finally { ReindexingAPSections = false; }
        }

        public static void OnSongSectionsRebuilt(Il2CppSystem.Collections.Generic.List<SongSection> sections)
        {
            if (!ReindexingAPSections && sections != null && sections.Pointer == CloneHeroMappings.GetSongSections()?.Pointer)
                APPatches.HasAvailableAPSongUpdate = true;
        }

        public static void RemoveAPListViewSongs(SongSelect menu,
            Il2CppSystem.Collections.Generic.List<SongSection> sections)
        {
            SelectedSection = null;
            SelectedSong = null;
            var selectedIndex = CloneHeroMappings.GetMenuScrollOffset(menu) + menu.GetSelectedMenuIndex();
            foreach (var section in sections)
            {
                var offset = selectedIndex - CloneHeroMappings.GetSectionStartIndex(section);
                if (offset < 0 || selectedIndex > CloneHeroMappings.GetSectionEndIndex(section)) continue;
                SelectedSection = CloneHeroMappings.GetSectionName(section);
                var songs = CloneHeroMappings.GetSectionSongs(section);
                if (offset > 0 && offset <= songs.Count)
                    SelectedSong = songs[offset - 1].ChecksumString;
                break;
            }

            foreach (var section in APSections.Values)
            {
                if (CloneHeroMappings.IsSectionCollapsed(section.Section)) CollapsedHeaders.Add(section.Key);
                else CollapsedHeaders.Remove(section.Key);
            }
            for (var i = sections.Count - 1; i >= 0; i--)
                if (APSections.ContainsKey(sections[i].Pointer))
                    sections.RemoveAt(i);
            APSections.Clear();
        }

        public static void InsertAPListViewSongs(APConnectionContainer container, SongSelect menu,
            Il2CppSystem.Collections.Generic.List<SongSection> sections)
        {
            var filteredHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in sections)
                foreach (var song in CloneHeroMappings.GetSectionSongs(section))
                    filteredHashes.Add(song.ChecksumString);
            if (!container.IsSessionConnected)
                return;

            var AvailableSongs = container.GetAvailableSongs(song => filteredHashes.Contains(song.ChecksumString), out var MissingInstrument, out _);
            int insertIndex = 0;
            var goal = container.SlotData.GoalData;
            if (goal.IsSongUnlocked(container) && goal.HasAvailableLocations(container) &&
                container.SongHashLookup.TryGetValue(goal.GetActiveHash(container), out var goalSong))
                InsertSection("GOAL", $"GOAL SONG: {goal.PoolName.ToUpperInvariant()}", new[] { goalSong });
            PrintSongsList(AvailableSongs, false);
            PrintSongsList(MissingInstrument, true);

            void PrintSongsList(List<SongAPData> songs, bool MissingInstrument)
            {
                var pools = songs.GroupBy(song => song.PoolName)
                    .OrderBy(pool => container.SlotData.Pools[pool.Key].instrument.GetDescription(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(pool => pool.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var pool in pools)
                {
                    var entries = pool.Select(song => container.SongHashLookup[song.GetActiveHash(container)])
                        .OrderBy(song => song.Name_StrippedTags, StringComparer.OrdinalIgnoreCase);
                    var title = MissingInstrument
                        ? $"AP: [LOCKED] {pool.Key.ToUpperInvariant()}"
                        : $"AP: {pool.Key.ToUpperInvariant()}";
                    InsertSection("POOL:" + pool.Key, title, entries);
                }
            }

            void InsertSection(string key, string title, IEnumerable<SongEntry> songs)
            {
                var section = CreateSongSection(title, songs);
                CloneHeroMappings.SetSectionCollapsed(section, CollapsedHeaders.Contains(key));
                APSections[section.Pointer] = (key, section);
                sections.Insert(insertIndex++, section);
            }
        }

        public static void RefreshSongLibrary(this SongSelect menu)
        {
            if (menu == null || !menu.isActiveAndEnabled)
                return;
            var sections = CloneHeroMappings.GetSongSections();
            var selectedIndex = 0;
            foreach (var section in sections)
            {
                if (CloneHeroMappings.GetSectionName(section) != SelectedSection) continue;
                selectedIndex = CloneHeroMappings.GetSectionStartIndex(section);
                var songs = CloneHeroMappings.GetSectionSongs(section);
                if (SelectedSong != null && !CloneHeroMappings.IsSectionCollapsed(section))
                    for (var i = 0; i < songs.Count; i++)
                        if (string.Equals(songs[i].ChecksumString, SelectedSong, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex += i + 1;
                            break;
                        }
                break;
            }
            CloneHeroMappings.RefreshSongList(menu);
            CloneHeroMappings.SelectMenuIndex(menu, selectedIndex, true);
        }

        public static bool TrySetArchipelagoMenuEntry(MainMenu menu)
        {
            if (menu == null)
                return false;
            var menuStrings = menu.menuStrings;
            var textObjects = menu.textObjects;
            if (menuStrings != null && menuStrings.Length > ArchipelagoMenuIndex &&
                menuStrings[ArchipelagoMenuIndex] == ArchipelagoMenuKey && ArchipelagoMenuRect != null)
                return true;
            if (menuStrings == null || textObjects == null ||
                menuStrings.Length != textObjects.Length || textObjects.Length <= ArchipelagoMenuIndex)
                return false;
            var label = textObjects[ArchipelagoMenuIndex];
            if (label == null || label.transform.parent == null)
                return false;

            label.transform.parent.gameObject.name = "archipelago";
            label.text = "ARCHIPELAGO";
            NativeArchipelagoMenuKey ??= (Il2CppSystem.String)ArchipelagoMenuKey;
            menuStrings.Cast<Il2CppSystem.Array>().SetValue(NativeArchipelagoMenuKey, ArchipelagoMenuIndex);
            ArchipelagoMenuRect = label.transform.parent.GetComponent<RectTransform>();
            return true;
        }

        public static void RestoreArchipelagoMenuLabel(MainMenu menu)
        {
            if (menu == null || menu.menuStrings == null || menu.textObjects == null ||
                menu.menuStrings.Length <= ArchipelagoMenuIndex || menu.textObjects.Length <= ArchipelagoMenuIndex ||
                NativeArchipelagoMenuKey == null)
                return;
            if (menu.menuStrings[ArchipelagoMenuIndex] != ArchipelagoMenuKey)
                menu.menuStrings.Cast<Il2CppSystem.Array>().SetValue(NativeArchipelagoMenuKey, ArchipelagoMenuIndex);
            var label = menu.textObjects[ArchipelagoMenuIndex];
            if (label != null && label.text != "ARCHIPELAGO")
                label.text = "ARCHIPELAGO";
        }

        public static bool IsArchipelagoMenuClicked(MainMenu menu)
        {
            if (menu == null || menu.textObjects == null || menu.textObjects.Length <= ArchipelagoMenuIndex ||
                !Input.GetMouseButtonDown(0))
                return false;

            return ArchipelagoMenuRect != null && ArchipelagoMenuRect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(ArchipelagoMenuRect, Input.mousePosition, null);
        }

    }
}
