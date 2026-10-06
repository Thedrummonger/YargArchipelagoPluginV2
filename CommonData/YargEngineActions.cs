using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YARG;
using YARG.Core;
using YARG.Core.Game;
using YARG.Scores;
using YARG.Core.Audio;
//Don't Let visual studios lie to me these are needed
using YARG.Core.Engine;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.Utility;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
//----------------------------------------------------
using YARG.Gameplay.Player;
using YARG.Localization;
using YARG.Menu.Dialogs;
using YARG.Menu.Filters;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Song;
using YARG.Settings;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public static class EngineActions
    {
        public static bool IsSupportedInstrument(Instrument source, out SupportedInstrument? target)
        {
            if (Enum.TryParse<SupportedInstrument>(source.ToString(), out var result))
            {
                target = result;
                return true;
            }
            target = null;
            return false;
        }

        public static SupportedDifficulty GetSupportedDifficulty(Difficulty source)
        {
            if (source > Difficulty.Expert)
                return SupportedDifficulty.Expert;
            if (source < Difficulty.Easy)
                return SupportedDifficulty.Easy;
            return (SupportedDifficulty)(int)source; //Easy starts at 1, so does SupportedDifficulty. Beginner is 0 which the apworld does not support
        }
        public static IEnumerable<APPlayerResult> GetAPPlayerResults(this IEnumerable<BasePlayer> Players)
        {
            foreach (var player in Players)
            {
                IsSupportedInstrument(player.Player.Profile.CurrentInstrument, out var instrument);
                if (player.Player.Profile.CurrentInstrument == Instrument.EliteDrums)
                    instrument = SupportedInstrument.ProDrums;
                yield return new APPlayerResult
                {
                    CurrentInstrument = instrument,
                    CurrentDifficulty = GetSupportedDifficulty(player.Player.Profile.CurrentDifficulty),
                    Stars = player.Stars,
                    IsGoldStars = StarAmountHelper.GetStarsFromInt((int)player.Stars) == StarAmount.StarGold,
                    IsFc = player.IsFc
                };
            }
        }

        public static string GetSongHash(SongEntry song) => Convert.ToBase64String(song.Hash.HashBytes);
        public static string GetSongHash(GameManager gameManager) => GetSongHash(gameManager.Song);
        public static string GetSongDisplayName(SongEntry song) => $"{song.Name} by {song.Artist}";
        public static StringComparer SongHashComparer => StringComparer.Ordinal;


        public static APSongResult GetSongResults(GameManager gameManager) => new APSongResult
        {
            Hash = Convert.ToBase64String(gameManager.Song.Hash.HashBytes),
            Name = $"{gameManager.Song.Name} by {gameManager.Song.Artist}",
            IsPractice = gameManager.IsPractice,
            PlayerHasFailed = gameManager.PlayerHasFailed,
            BandScore = gameManager.BandScore,
            Players = gameManager.Players.GetAPPlayerResults().ToList()
        };

        public static void FailedSong(GameManager gameManager)
        {
            var parent = ArchipelagoPlugin.APcontainer;
            if (!parent.IsSessionConnected || parent.seedConfig is null || !parent.seedConfig.SendDlOnSongFail())
                return;

            if (CanFailSong() && !gameManager.IsPractice && !gameManager.PlayerHasFailed)
            {
                parent.DeathLinkService?.SendDeathLink(new DeathLink(parent.GetSession().Players.ActivePlayer.Name, $"Failed Song {gameManager.Song.Name} by {gameManager.Song.Artist}"));
            }
        }

        public static bool CanFailSong()
        {
            return SettingsManager.Settings.NoFail.Value == YARG.Gameplay.HUD.NoFailMode.Off;
        }

        public static string SongExportFile => Path.Combine(DataFolder, "SongExport.json");

        private static readonly IEnumerable<Instrument> AllYargInstruments = Enum.GetValues(typeof(Instrument)).Cast<Instrument>();

        public static SongExportData GetSongExportData(SongEntry song)
        {
            var Entry = new SongExportData()
            {
                Artist = RichTextUtils.StripRichTextTags(song.Artist),
                Name = RichTextUtils.StripRichTextTags(song.Name),
                SongChecksum = Convert.ToBase64String(song.Hash.HashBytes),
                Difficulties = new Dictionary<SupportedInstrument, int>(),
                Source = song.Source.Original,
                Album = song.Album,
                Genre = song.Genre,
                Charter = song.Charter,
                Time = Math.Round(song.SongLengthSeconds)
            };
            foreach (var instrument in AllYargInstruments)
            {
                if (!song.HasInstrument(instrument) || !IsSupportedInstrument(instrument, out var supportedInstrument))
                    continue;
                Entry.Difficulties[supportedInstrument.Value] = song[instrument].Intensity < 0 ? 0 : song[instrument].Intensity;
            }
            return Entry;
        }

        public static Dictionary<string, SongEntry> GetSongLookup()
        {
            var songs = new Dictionary<string, SongEntry>();
            foreach (var song in SongContainer.Songs)
                songs[Convert.ToBase64String(song.Hash.HashBytes)] = song;
            return songs;
        }

        public static void DumpAvailableSongs()
        {
            var SongData = GetYargSongExportData();
            SongExportData.WriteToFile(SongExportFile, SongData.Values);
        }
        public static Dictionary<string, SongExportData> GetYargSongExportData()
        {
            Dictionary<string, SongExportData> SongData = new Dictionary<string, SongExportData>();
            foreach (var song in GetSongLookup())
                SongData[song.Key] = GetSongExportData(song.Value);
            return SongData;
        }

        private static int GetListViewIndex(List<ViewType> listView, string Key)
        {
            var primaryField = AccessTools.Field(typeof(ButtonViewType), "_text");
            int insertIndex = -1;
            for (int i = 0; i < listView.Count; i++)
            {
                if (listView[i] is ButtonViewType button && (string)primaryField.GetValue(button) == Key)
                {
                    insertIndex = i + 1;
                    break;
                }
            }
            return insertIndex;
        }

        private static int ButtonInd = 100;

        static HashSet<string> collapsedHeaders = [];
        public static void InsertAPListViewSongs(APConnectionContainer container, MusicLibraryMenu menu, List<ViewType> listView)
        {
            if (!container.IsSessionConnected) 
                return;

            int insertIndex = GetListViewIndex(listView, Localize.Key("Menu.MusicLibrary.Playlists"));
            if (insertIndex < 0) 
                return;

            ButtonInd = 100;

            var AvailableSongs = container.GetAvailableSongs(FiltersMenu.ActiveFilterPredicate, out var AvailableMissingInst, out var AllKnownSongs);
            bool GoalSongUnlocked = container.SlotData.GoalData.IsSongUnlocked(container);

            listView.Insert(insertIndex++, new ButtonViewType("ARCHIPELAGO".ToRainbowString() + " SONGS", "MusicLibraryIcons[Recommended]", 
                () => menu.APRefreshAndReselect(true), ButtonInd++, "Refresh AP Song List"));

            if (GoalSongUnlocked && container.SlotData.GoalData.HasAvailableLocations(container)
                 && container.SlotData.GoalData.HasSongEntry(container, out var GoalSong))
            {
                var Pool = container.SlotData.GoalData.PoolName;
                var GoalHidden = collapsedHeaders.Contains("GOAL");
                listView.Insert(insertIndex++, new SortHeaderViewType($"GOAL SONG: {Pool.ToUpper()}", 1 , "Goal Song",
                    [GoalSong], GoalHidden, () => { 
                        if (!collapsedHeaders.Remove("GOAL")) 
                            collapsedHeaders.Add("GOAL"); 
                        menu.APRefreshAndReselect(true);
                    }));
                if (!GoalHidden)
                    listView.Insert(insertIndex++, new APSongViewType(menu, GoalSong, false));
            }

            insertIndex = PrintSongsList(container, menu, listView, AvailableSongs, insertIndex);

            if (AvailableMissingInst.Any())
                listView.Insert(insertIndex++, new SortHeaderViewType("SONGS MISSING INSTRUMENTS", AvailableMissingInst.Count(), "missing instruments",
                    [.. AvailableMissingInst.Select(x => x.GetSongEntry(container))], !container.seedConfig.ShowMissingInstruments, 
                    () => ToggleShowMissingInst(container, menu)));

            if (container.seedConfig.ShowMissingInstruments)
                insertIndex = PrintSongsList(container, menu, listView, AvailableMissingInst, insertIndex, Color.red);


            listView.Insert(insertIndex++, new SortHeaderViewType("GOAL".ToRainbowString() + " INFO", 0, "goal info",
                [], !container.seedConfig.ShowGoalStatus, () =>
            {
                container.seedConfig.ShowGoalStatus = !container.seedConfig.ShowGoalStatus;
                container.seedConfig.Save();
                menu.APRefreshAndReselect(true);
            }));

            if (container.seedConfig.ShowGoalStatus)
            {
                listView.Insert(insertIndex++, new ButtonViewType($"Goal Conditions Met: {GoalSongUnlocked.ToColoredString()}",
                    "MusicLibraryIcons[Recommended]", () => FormHelpers.ShowGoalConditionStatus(container), ButtonInd++, "Show Goal Condition Status"));
                insertIndex = AddMacGuffinEntry(StaticItems.SongCompletion, "Setlist", container.SlotData.SetlistNeededForGoal, listView, container, insertIndex);
                insertIndex = AddMacGuffinEntry(StaticItems.FamePoint, "Fame", container.SlotData.FamePointsForGoal, listView, container, insertIndex);

                if (container.GoalItemInPool(out var GoalItemRecieved, out _))
                    listView.Insert(insertIndex++, 
                        new ButtonViewType($"Goal Item", 
                        "MusicLibraryIcons[Recommended]", () => FormHelpers.ShowGoalRecieveMessage(container, GoalItemRecieved), ButtonInd++, 
                        (GoalItemRecieved ? "Found".ToColoredString(Color.green) : "Missing".ToColoredString(Color.red))));
                listView.Insert(insertIndex++, new ButtonViewType($"Reveal Goal Song", "MusicLibraryIcons[Recommended]", 
                    () => DialogManager.Instance.ShowMessage("GOAL SONG", container.SlotData.GoalData.GetDisplayName(container, true)), ButtonInd++, ""));
            }

            listView.Insert(insertIndex++, new SortHeaderViewType("POOL".ToRainbowString() + " INFO", 0, "pool info",
                [], !container.seedConfig.ShowPoolInfo, () =>
                {
                    container.seedConfig.ShowPoolInfo = !container.seedConfig.ShowPoolInfo;
                    container.seedConfig.Save();
                    menu.APRefreshAndReselect(true);
                }));

            if (container.seedConfig.ShowPoolInfo)
            {
                foreach (var pool in container.SlotData.Pools)
                {
                    var poolName = pool.Key;
                    var poolData = pool.Value;
                    listView.Insert(insertIndex++, new ButtonViewType($"{poolName.ToUpper()}", "MusicLibraryIcons[Recommended]",
                        () => FormHelpers.ShowPoolData(container, poolName), ButtonInd++, $"Show {poolName.ToUpper()} Requirements"));
                }
            }

            listView.Insert(insertIndex++, new SortHeaderViewType("ARCHIPELAGO".ToRainbowString() + " MENU", 0, "ap menu",
                [], !container.seedConfig.ShowAPMenu, () =>
            {
                container.seedConfig.ShowAPMenu = !container.seedConfig.ShowAPMenu;
                container.seedConfig.Save();
                menu.APRefreshAndReselect(true);
            }));

            if (container.seedConfig.ShowAPMenu)
            {
                if (AllKnownSongs.Any())
                {
                    insertIndex = AddUseMenu(StaticItems.SwapPick, listView, container, insertIndex, SwapSongMenu.ShowMenu);
                    insertIndex = AddUseMenu(StaticItems.SwapRandom, listView, container, insertIndex, SwapSongMenu.ShowMenu);
                    insertIndex = AddUseMenu(StaticItems.LowerDifficulty, listView, container, insertIndex, LowerDifficultyMenu.ShowMenu);
                }

                if (container.seedConfig.EnergyLinkMode > EnergyLinkType.disabled || true)
                    listView.Insert(insertIndex++, new ButtonViewType($"Open Energy Shop", "MusicLibraryIcons[Recommended]",
                        () => EnergyLinkShop.ShowMenu(container), ButtonInd++, "Purchase Filler Items with Energy"));
            }
        }

        private static void ToggleShowMissingInst(APConnectionContainer container, MusicLibraryMenu menu)
        {
            container.seedConfig.ShowMissingInstruments = !container.seedConfig.ShowMissingInstruments;
            container.seedConfig.Save();
            menu.APRefreshAndReselect(true);
        }

        private static int AddMacGuffinEntry(StaticItems Type, string Name, int Needed, List<ViewType> L, APConnectionContainer C, int I)
        {
            if (Needed <= 0) return I;
            var insertIndex = I;
            var current = C.ApItemsRecieved.Count(x => x.Type == Type);
            L.Insert(insertIndex++, new ButtonViewType($"{Name} Goal", "MusicLibraryIcons[Recommended]",
                () => FormHelpers.ShowMacGuffinStatus(current, Needed, Name), ButtonInd++, $"{current}/{Needed}"));
            return insertIndex;
        }

        private static int AddUseMenu(StaticItems Type, List<ViewType> L, APConnectionContainer C, int I, Action<APConnectionContainer, StaticYargAPItem> Show)
        {
            var insertIndex = I;
            var Items = C.GetAllAquiredActionItems().Where(x => x.Type == Type && !C.seedConfig.ApItemsUsed.Contains(x));
            if (Items.Any())
            {
                var ToUse = Items.First();
                L.Insert(insertIndex++, new ButtonViewType($"Use {Type.GetDescription()}", "MusicLibraryIcons[Recommended]",
                    () => Show(C, ToUse), ButtonInd++, $"{Items.Count()} Remaining"));
            }
            return insertIndex;
        }

        private static int PrintSongsList(APConnectionContainer container, MusicLibraryMenu menu, List<ViewType> listView, IEnumerable<SongAPData> toPrint, int CurIndex, Color? Color = null)
        {
            int insertIndex = CurIndex;
            foreach (var pool in toPrint
                .OrderBy(e => e.GetPool(container.SlotData).instrument.GetDescription(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.PoolName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(e => e.PoolName))
            {
                string PoolName = pool.Key.ToUpper();
                bool IsCollapsed = collapsedHeaders.Contains(PoolName);
                if (Color.HasValue)
                    PoolName = PoolName.ToColoredString(Color.Value);

                var poolSongs = pool.Select(e => (e.GetSongEntry(container), e)).OrderBy(s => s.Item1.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                listView.Insert(insertIndex++, new SortHeaderViewType($"AP: {PoolName}", poolSongs.Length, "ap pool",
                    [.. poolSongs.Select(x => x.Item1)], IsCollapsed, () => { 
                        if (!collapsedHeaders.Remove(PoolName)) 
                            collapsedHeaders.Add(PoolName);
                        menu.APRefreshAndReselect(true);
                    }));

                if (IsCollapsed) { continue; }
                foreach (var song in poolSongs)
                    listView.Insert(insertIndex++, new APSongViewType(menu, song.Item1, song.e.IsHinted(container, out _)));
            }
            return insertIndex;
        }

        /// <summary>
        /// Grants star power to all active players when an Archipelago star power item is received.
        /// </summary>
        public static void ApplyStarPowerItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            handler.LogInfo?.Invoke($"Gaining Star Power");
            MethodInfo method = AccessTools.Method(typeof(BaseEngine), "GainStarPower");
            foreach (var player in current.Players)
                method.Invoke(player.BaseEngine, new object[] { player.BaseEngine.TicksPerQuarterSpBar });

        }
        /// <summary>
        /// Reduces the rock meter for all active players by 1/4 when an Archipelago trap item is received.
        /// </summary>
        public static void ApplyRockMetertrapItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            handler.LogInfo?.Invoke($"Reducing Rock Meter");
            foreach (var player in current.Players)
                AddHappiness(player, -0.25f);

        }
        /// <summary>
        /// Applies the effects of a received DeathLink, either reducing rock meter or forcing instant fail based on settings.
        /// </summary>
        public static bool ApplyDeathLink(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out _, out _)) return false;
            switch (handler.seedConfig.DeathLinkMode)
            {
                case DeathLinkType.rock_meter:
                    SetBandHappiness(handler, 0.02f);
                    break;
                case DeathLinkType.instant_fail:
                    ForceFailSong(handler);
                    break;
                default:
                    return false;
            }
            return true;
        }
        /// <summary>
        /// Forces the current song to restart by opening the pause menu and triggering restart.
        /// </summary>
        public static void ForceRestartSong(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            handler.ResetBuffer();
            try
            {
                MonoSingleton<GlobalVariables>.Instance.LoadScene(SceneIndex.Gameplay);
            }
            catch (Exception e)
            {
                handler.LogError?.Invoke($"Failed to force restart song\n{e}");
            }
        }
        /// <summary>
        /// Forces the current song to fail without triggering a DeathLink send. Reimplements song fail behavior to avoid recursion.
        /// </summary>
        public static async void ForceFailSong(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var gameManager, out _) || gameManager.IsPractice)
                return;

            gameManager.PlayerHasFailed = true;
            try
            {
                var mixerObj = AccessTools.Field(typeof(GameManager), "_mixer")?.GetValue(gameManager);
                var fade = mixerObj?.GetType().GetMethod("FadeOut", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fade?.Invoke(mixerObj, new object[] { GameManager.SONG_END_DELAY });
            }
            catch { }
            await UniTask.Delay(TimeSpan.FromSeconds(GameManager.SONG_END_DELAY));
            GlobalAudioHandler.PlayVoxSample(VoxSample.FailSound);
            gameManager.Pause(true);
        }

        /// <summary>
        /// Sets all players' happiness to the specified value or their starting happiness if no value provided.
        /// </summary>
        public static void SetBandHappiness(APConnectionContainer handler, float? delta = null)
        {
            if (!handler.IsInSong(out var gameManager, out _) || gameManager.IsPractice)
                return;
            foreach (var player in gameManager.Players)
            {
                var EngineContainer = player.GetEngineContainer();
                EngineContainer.SetHappiness(delta ?? EngineContainer.RockMeterPreset.StartingHappiness);
            }
        }

        private static MethodInfo _addHappinessMethod;
        private static PropertyInfo _happinessProperty;
        private static MethodInfo _updateHappinessMethod;
        private static FieldInfo _engineContainerField;
        private static FieldInfo _allEnginesField; private static FieldInfo _containerEngineManagerField;

        /// <summary>
        /// Gets the parent EngineManager for a container.
        /// </summary>
        public static EngineManager GetEngineManager(this EngineManager.EngineContainer container)
        {
            if (_containerEngineManagerField == null)
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                _containerEngineManagerField = typeof(EngineManager.EngineContainer).GetField("EngineManager", flags)
                    ?? typeof(EngineManager.EngineContainer).GetField("_engineManager", flags);
            }
            return (EngineManager)_containerEngineManagerField?.GetValue(container);
        }

        /// <summary>
        /// Gets the EngineContainer for a player.
        /// </summary>
        public static EngineManager.EngineContainer GetEngineContainer(this BasePlayer player)
        {
            if (_engineContainerField == null) _engineContainerField = typeof(BasePlayer).GetField("EngineContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            return (EngineManager.EngineContainer)_engineContainerField.GetValue(player);
        }

        /// <summary>
        /// Gets all EngineContainers in the given EngineManager.
        /// Note: This could also be done by looping through players.
        /// </summary>
        public static List<EngineManager.EngineContainer> GetAllEngines(this EngineManager engineManager)
        {
            if (_allEnginesField == null) _allEnginesField = typeof(EngineManager).GetField("_allEngines", BindingFlags.NonPublic | BindingFlags.Instance);
            return (List<EngineManager.EngineContainer>)_allEnginesField?.GetValue(engineManager);
        }
        /// <summary>
        /// Adds happiness to a player's rock meter. This method will trigger any harmony patches applied to AddHappiness.
        /// </summary>
        public static void AddHappiness(this BasePlayer player, float delta) => AddHappiness(player.GetEngineContainer(), delta);
        /// <summary>
        /// Adds happiness to an engine container. This method will trigger any harmony patches applied to AddHappiness.
        /// </summary>
        public static void AddHappiness(this EngineManager.EngineContainer container, float delta)
        {
            if (_addHappinessMethod == null) _addHappinessMethod = typeof(EngineManager.EngineContainer).GetMethod("AddHappiness", BindingFlags.NonPublic | BindingFlags.Instance);
            _addHappinessMethod?.Invoke(container, new object[] { delta });
        }

        /// <summary>
        /// Sets a player's happiness to a specific value.
        /// </summary>
        public static void SetHappiness(this BasePlayer player, float value) => SetHappiness(player.GetEngineContainer(), value);
        /// <summary>
        /// Sets an engine container's happiness to a specific value.
        /// </summary>
        public static void SetHappiness(this EngineManager.EngineContainer container, float value)
        {
            if (_happinessProperty == null) _happinessProperty = typeof(EngineManager.EngineContainer).GetProperty("Happiness", BindingFlags.Public | BindingFlags.Instance);
            if (_updateHappinessMethod == null) _updateHappinessMethod = typeof(EngineManager).GetMethod("UpdateHappiness", BindingFlags.NonPublic | BindingFlags.Instance);
            value = Mathf.Clamp(value, -3f, 1f);
            _happinessProperty?.SetValue(container, value);
            var engineManager = container.GetEngineManager();
            _updateHappinessMethod?.Invoke(engineManager, null);
        }
        /// <summary>
        /// Finds and returns the engine container with the lowest happiness value.
        /// </summary>
        public static EngineManager.EngineContainer GetLowestHappiness(this EngineManager engineManager)
        {
            EngineManager.EngineContainer lowestContainer = null;
            float lowestHappiness = float.MaxValue;

            foreach (var container in engineManager.GetAllEngines())
            {
                if (container.Happiness < lowestHappiness)
                {
                    lowestHappiness = container.Happiness;
                    lowestContainer = container;
                }
            }
            return lowestContainer;
        }
        /// <summary>
        /// Prevents song failure by boosting the lowest player's happiness until average happiness reaches 0.25 (quarter bar).
        /// Repeatedly adds single-note-hit worth of happiness to the lowest player.
        /// </summary>
        public static void PreventSongFail(this EngineManager engineManager)
        {
            /// <see cref="EngineManager.EngineContainer"/> private const HAPPINESS_PER_NOTE_HIT = 1f / 168f
            const float HAPPINESS_PER_NOTE_HIT = 1f / 168f;
            const float TARGET_HAPPINESS = 0.25f;
            if (_happinessProperty == null) _happinessProperty = typeof(EngineManager.EngineContainer).GetProperty("Happiness", BindingFlags.Public | BindingFlags.Instance);

            while (engineManager.Happiness < TARGET_HAPPINESS)
            {
                EngineManager.EngineContainer lowestContainer = GetLowestHappiness(engineManager);

                if (lowestContainer == null)
                    break;

                float newHappiness = Mathf.Clamp(lowestContainer.Happiness + HAPPINESS_PER_NOTE_HIT, -3f, 1f);
                _happinessProperty.SetValue(lowestContainer, newHappiness);
            }
        }

        /// <summary>
        /// Forces the player to exit the current song immediately. Alternative to failing a song for Stable.
        /// </summary>
        private static void ForceExitSong(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            try
            {
                handler.LogInfo?.Invoke($"Forcing Quit");
                current.ForceQuitSong();
            }
            catch (Exception e)
            {
                handler.LogInfo?.Invoke($"Failed to force exit song\n{e}");
            }
        }

        private static readonly Type MenuType = typeof(MusicLibraryMenu);

        private static readonly Action<MusicLibraryMenu> _refresh =
            AccessTools.MethodDelegate<Action<MusicLibraryMenu>>(
                AccessTools.Method(MenuType, "Refresh")
            );

        private static readonly Func<MusicLibraryMenu, int> _getSelectedIndex =
            AccessTools.MethodDelegate<Func<MusicLibraryMenu, int>>(
                AccessTools.PropertyGetter(MenuType, "SelectedIndex")
            );

        private static readonly Action<MusicLibraryMenu, int> _setSelectedIndex =
            AccessTools.MethodDelegate<Action<MusicLibraryMenu, int>>(
                AccessTools.PropertySetter(MenuType, "SelectedIndex")
            );
        public static void APRefreshAndReselect(this MusicLibraryMenu menu, bool strictKeepPosition)
        {
            if (strictKeepPosition)
            {
                int selectedIndex = _getSelectedIndex(menu);
                _refresh(menu);
                _setSelectedIndex(menu, selectedIndex);
            }
            else
                menu.RefreshAndReselect();
        }

        public static readonly FieldInfo NavigatableButton_onClick = AccessTools.Field(typeof(NavigatableButton), "_onClick");
        public static readonly FieldInfo NavigationGroup_navigatables = AccessTools.Field(typeof(NavigationGroup), "_navigatables");
        public static NavigatableButton FindNav(GameObject root, string method)
        {
            foreach (var b in root.GetComponentsInChildren<NavigatableButton>(true))
            {
                var ev = (UnityEngine.UI.Button.ButtonClickedEvent)NavigatableButton_onClick.GetValue(b);
                for (int i = 0, n = ev.GetPersistentEventCount(); i < n; i++)
                    if (ev.GetPersistentMethodName(i) == method) return b;
            }
            return null;
        }

        public static void TrySetText(GameObject root, string text)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                var t = c.GetType();
                var p = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string) && p.CanWrite) { p.SetValue(c, text, null); return; }
                var f = t.GetField("text", BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(string)) { f.SetValue(c, text); return; }
            }
        }

        public static bool IsPreventingSongFail = false;
        public static bool TryPreventSongFail(EngineManager __instance)
        {
            if (__instance.Happiness > 0.0f)
                return true;

            if (!ArchipelagoPlugin.APcontainer.IsSessionConnected)
                return true;

            if (!ArchipelagoPlugin.APcontainer.IsInSong(out var gameManager, out _))
                return true;

            if (IsPreventingSongFail)
                return false;

            IsPreventingSongFail = true;

            if (!gameManager.PlayerHasFailed && gameManager.CouldProductLocationCheck(ArchipelagoPlugin.APcontainer, out _))
                FillerItems.TryUseFailPrevention(ArchipelagoPlugin.APcontainer, () => PreventSongFail(__instance));

            IsPreventingSongFail = false;

            return true;
        }

    }
}
