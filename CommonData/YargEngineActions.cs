using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using YARG;
using YARG.Core;
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
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Song;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public static class YargEngineActions
    {
        public static void DumpAvailableSongs()
        {
            var SongData = GetYargSongExportData();
            if (!Directory.Exists(DataFolder)) Directory.CreateDirectory(DataFolder);
            File.WriteAllText(SongExportFile, JsonConvert.SerializeObject(SongData.Values.ToArray(), Formatting.Indented));
        }
        public static Dictionary<string, SongExportData> GetYargSongExportData()
        {
            Dictionary<string, SongExportData> SongData = new Dictionary<string, SongExportData>();
            foreach(var song in SongContainer.Songs)
            {
                var Hash = Convert.ToBase64String(song.Hash.HashBytes);
                SongData[Hash] = SongExportData.FromSongEntry(song);
            }
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

            var AvailableSongs = container.GetAvailableSongs(out var AvailableMissingInst, out var AllKnownSongs);
            bool GoalSongUnlocked = container.SlotData.GoalData.IsSongUnlocked(container);

            listView.Insert(insertIndex++, new ButtonViewType("ARCHIPELAGO".ToRainbowString() + " SONGS", "MusicLibraryIcons[Recommended]", 
                () => menu.APRefreshAndReselect(true), ButtonInd++, "Refresh AP Song List"));

            if (GoalSongUnlocked && container.SlotData.GoalData.HasAvailableLocations(container)
                 && container.SlotData.GoalData.HadYargSongEntry(container, out var GoalSong))
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
                    listView.Insert(insertIndex++, new APSongViewType(menu, GoalSong));
            }

            insertIndex = PrintSongsList(container, menu, listView, AvailableSongs, insertIndex);

            if (AvailableMissingInst.Any())
                listView.Insert(insertIndex++, new SortHeaderViewType("SONGS MISSING INSTRUMENTS", AvailableMissingInst.Count(), "missing instruments",
                    [.. AvailableMissingInst.Select(x => x.GetYargSongEntry(container))], !container.seedConfig.ShowMissingInstruments, 
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
                listView.Insert(insertIndex++, new ButtonViewType($"Goal Conditions Met: {GoalSongUnlocked.ToYargColoredString()}",
                    "MusicLibraryIcons[Recommended]", () => ShowGoalConditionStatus(container), ButtonInd++, "Show Goal Condition Status"));
                insertIndex = AddMacGuffinEntry(StaticItems.SongCompletion, "Setlist", container.SlotData.SetlistNeededForGoal, listView, container, insertIndex);
                insertIndex = AddMacGuffinEntry(StaticItems.FamePoint, "Fame", container.SlotData.FamePointsForGoal, listView, container, insertIndex);

                if (container.GoalItemInPool(out var GoalItemRecieved, out var recieveInfo))
                    listView.Insert(insertIndex++, 
                        new ButtonViewType($"Goal Item", 
                        "MusicLibraryIcons[Recommended]", () => ShowGoalRecieveMessage(container, GoalItemRecieved, recieveInfo), ButtonInd++, 
                        (GoalItemRecieved ? "Found".ToYargColoredString(Color.green) : "Missing".ToYargColoredString(Color.red))));
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
                        () => ShowPoolData(container, poolName), ButtonInd++, $"Show {poolName.ToUpper()} Requirements"));
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

        private static void ShowGoalConditionStatus(APConnectionContainer container)
        {
            bool GoalMet = container.SlotData.GoalData.IsSongUnlocked(container);
            string Header = GoalMet ?
                "Goal Conditions Met!\nPlay your goal song to complete the seed!" :
                "Goal Conditions NOT Met!\nComplete all the conditions below to unlock your goal song!";

            StringBuilder Conditions = new StringBuilder();
            if (container.GoalItemInPool(out bool ItemFound, out _))
                Conditions.AppendLine($"Find Your Goal Unlock Item.")
                        .AppendLine($"Found: {ItemFound}").AppendLine();
            if (container.SlotData.SetlistNeededForGoal > 0)
                Conditions.AppendLine($"Complete {container.SlotData.SetlistNeededForGoal} Songs.")
                        .AppendLine($"Current Completion {container.ApItemsRecieved.Count(x => x.Type == StaticItems.SongCompletion)}").AppendLine();
            if (container.SlotData.FamePointsForGoal > 0)
                Conditions.AppendLine($"Find {container.SlotData.FamePointsForGoal} Fame Points.")
                        .AppendLine($"Current Fame {container.ApItemsRecieved.Count(x => x.Type == StaticItems.FamePoint)}").AppendLine();

            DialogManager.Instance.ShowMessage(Header, Conditions.ToString());
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
                () => ShowMacGuffinStatus(current, Needed, Name), ButtonInd++, $"{current}/{Needed}"));
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
                    PoolName = PoolName.ToYargColoredString(Color.Value);

                var poolSongs = pool.Select(e => e.GetYargSongEntry(container)).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                listView.Insert(insertIndex++, new SortHeaderViewType($"AP: {PoolName}", poolSongs.Length, "ap pool",
                    poolSongs, IsCollapsed, () => { 
                        if (!collapsedHeaders.Remove(PoolName)) 
                            collapsedHeaders.Add(PoolName);
                        menu.APRefreshAndReselect(true);
                    }));

                if (IsCollapsed) { continue; }
                foreach (var song in poolSongs)
                    listView.Insert(insertIndex++, new APSongViewType(menu, song));
            }
            return insertIndex;
        }

        private static void ShowMacGuffinStatus(int Current, int Need, string Title)
        {
            if (Current < Need)
                APToastManager.ToastError($"{Title} goal not met!\nHas: {Current}\nNeed:{Need}");
            else
                APToastManager.ToastSuccess($"{Title} goal met!\nHas: {Current}\nNeed:{Need}");
        }

        private static void ShowGoalRecieveMessage(APConnectionContainer container, bool Recieved, BaseYargAPItem recieveInfo)
        {
            if (!Recieved)
            {
                APToastManager.ToastError($"Your goal song unlock item has not been found!");
                return;
            }
            var Team = container.GetSession().Players.ActivePlayer.Team;
            var Player = container.GetSession().Players.GetPlayerInfo(Team, recieveInfo.SendingPlayerSlot);
            var LocationInfo = container.GetSession().Locations.GetLocationNameFromId(recieveInfo.SendingPlayerLocation, recieveInfo.SendingPlayerGame);
            DialogManager.Instance.ShowMessage("Goal Unlock Item Found!", $"Found by Player:\n{Player.Name}\n\nFrom Location:\n{LocationInfo}\n\nPlaying Game:\n{Player.Game}");
        }

        public static void ShowPoolData(APConnectionContainer container, string poolName)
        {
            if (!container.SlotData.Pools.TryGetValue(poolName, out var SongPool))
                return;
            ShowPoolData(container, $"SONG POOL: {poolName}" , SongPool);
        }
        public static void ShowPoolData(APConnectionContainer container, string Title, SongPool SongPool)
        {

            StringBuilder Result = new StringBuilder()
                .AppendLine($"REQUIRED INSTRUMENT: {SongPool.instrument.GetDescription()}")
                .AppendLine()
                .AppendLine($"REWARD 1 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {SongPool.completion_requirements.reward1_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {SongPool.completion_requirements.reward1_req.GetDescription()}")
                .AppendLine()
                .AppendLine($"REWARD 2 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {SongPool.completion_requirements.reward2_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {SongPool.completion_requirements.reward2_req.GetDescription()}");
            if (container.ReceivedInstruments.TryGetValue(SongPool.instrument, out var info))
            {
                var Player = info.GetPlayerInfo(container);
                Result.AppendLine().AppendLine($"{SongPool.instrument.GetDescription()} Recieved from").Append(Player.Name);
                if (Player.Slot > 0)
                {
                    var location = container.GetSession().Locations.GetLocationNameFromId(info.SendingPlayerLocation, Player.Game);
                    Result.AppendLine($" Playing {Player.Game}").AppendLine($"at {location}");
                }
            }
            DialogManager.Instance.ShowMessage(Title, Result.ToString());
        }
        /// <summary>
        /// Grants star power to all active players when an Archipelago star power item is received.
        /// </summary>
        public static void ApplyStarPowerItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            handler.logger.LogInfo($"Gaining Star Power");
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
            handler.logger.LogInfo($"Reducing Rock Meter");
            foreach (var player in current.Players)
                AddHappiness(player, -0.25f);

        }
        /// <summary>
        /// Applies the effects of a received DeathLink, either reducing rock meter or forcing instant fail based on settings.
        /// </summary>
        public static void ApplyDeathLink(APConnectionContainer handler, DeathLink deathLink)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            try
            {
                handler.logger.LogInfo($"Applying Death Link");
                switch (handler.seedConfig.DeathLinkMode)
                {
                    case CommonData.DeathLinkType.rock_meter:
                        SetBandHappiness(handler, 0.02f);
                        break;
                    case CommonData.DeathLinkType.instant_fail:
                        ForceFailSong(handler);
                        break;
                    default:
                        return;
                }
                APToastManager.ToastInformation($"DeathLink Received!\n\n{deathLink?.Source ?? "Debug"} {deathLink?.Cause ?? "Command"}");
            }
            catch (Exception e)
            {
                handler.logger.LogError($"Failed to apply deathlink\n{e}");
            }
        }
        /// <summary>
        /// Forces the current song to restart by opening the pause menu and triggering restart.
        /// </summary>
        public static void ForceRestartSong(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            try
            {
                MonoSingleton<GlobalVariables>.Instance.LoadScene(SceneIndex.Gameplay);
            }
            catch (Exception e)
            {
                handler.logger.LogError($"Failed to force restart song\n{e}");
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
            if (_containerEngineManagerField == null) _containerEngineManagerField = typeof(EngineManager.EngineContainer).GetField("_engineManager", BindingFlags.NonPublic | BindingFlags.Instance);
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
        /// Adds happiness without triggering harmony patches.
        /// </summary>
        public static void AddHappinessRaw(this BasePlayer player, float delta) => AddHappinessRaw(player.GetEngineContainer(), delta);
        /// <summary>
        /// Adds happiness without triggering harmony patches.
        /// </summary>
        public static void AddHappinessRaw(this EngineManager.EngineContainer container, float delta)
        {
            if (_happinessProperty == null) _happinessProperty = typeof(EngineManager.EngineContainer).GetProperty("Happiness", BindingFlags.Public | BindingFlags.Instance);
            if (_updateHappinessMethod == null) _updateHappinessMethod = typeof(EngineManager).GetMethod("UpdateHappiness", BindingFlags.NonPublic | BindingFlags.Instance);

            float newHappiness = Mathf.Clamp(container.Happiness + delta, -3f, 1f);
            _happinessProperty.SetValue(container, newHappiness);
            _updateHappinessMethod?.Invoke(container.GetEngineManager(), null);
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

            while (engineManager.Happiness < TARGET_HAPPINESS)
            {
                EngineManager.EngineContainer lowestContainer = GetLowestHappiness(engineManager);

                if (lowestContainer == null)
                    break;

                lowestContainer.AddHappinessRaw(HAPPINESS_PER_NOTE_HIT);
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
                handler.logger.LogInfo($"Forcing Quit");
                current.ForceQuitSong();
            }
            catch (Exception e)
            {
                handler.logger.LogInfo($"Failed to force exit song\n{e}");
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

    }
}
