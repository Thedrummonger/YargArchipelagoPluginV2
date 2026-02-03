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
using YARG.Menu.Persistent;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public static class YargEngineActions
    {
        public static void DumpAvailableSongs(SongCache SongCache)
        {
            var SongData = GetYargSongExportData(SongCache.Instruments);
            if (!Directory.Exists(CommonData.DataFolder)) Directory.CreateDirectory(CommonData.DataFolder);
            File.WriteAllText(CommonData.SongExportFile, JsonConvert.SerializeObject(SongData.Values.ToArray(), Formatting.Indented));
        }

        public static Dictionary<string, SongExportData> GetYargSongExportData(IReadOnlyDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> SongsByInstrument)
        {
            Dictionary<string, SongExportData> SongData = new Dictionary<string, SongExportData>();

            foreach (var instrument in SongsByInstrument)
            {
                if (!YargAPUtils.IsSupportedInstrument(instrument.Key, out var supportedInstrument)) continue;
                foreach (var Difficulty in instrument.Value)
                {
                    if (Difficulty.Key < 0) continue;
                    foreach (var song in Difficulty.Value)
                    {
                        var Hash = Convert.ToBase64String(song.Hash.HashBytes);
                        if (!SongData.ContainsKey(Hash))
                            SongData[Hash] = SongExportData.FromSongEntry(song);
                        SongData[Hash].Difficulties[supportedInstrument.Value] = Difficulty.Key;
                        SongData[Hash].YargSongEntry = song;
                    }
                }
            }
            return SongData;
        }

        public static bool UpdateRecommendedSongsMenu()
        {
            var Menu = UnityEngine.Object.FindObjectOfType<MusicLibraryMenu>();
            if (Menu == null || !Menu.gameObject.activeInHierarchy)
                return false;

            Menu.RefreshAndReselect();
            return true;
        }

        public static int GetListViewIndex(List<ViewType> listView, string Key)
        {
            string allSongsKey = Localize.Key("Menu.MusicLibrary.AllSongs");
            var primaryField = AccessTools.Field(typeof(CategoryViewType), "_primary");
            int insertIndex = -1;
            for (int i = 0; i < listView.Count; i++)
            {
                if (listView[i] is CategoryViewType cat && (string)primaryField.GetValue(cat) == allSongsKey)
                {
                    insertIndex = i;
                    break;
                }
            }
            return insertIndex;
        }

        public static void InsertAPListViewSongs(APConnectionContainer container, MusicLibraryMenu menu, List<ViewType> listView, IEnumerable<(SongEntry song, SongAPData APData)> entries)
        {
            int insertIndex = GetListViewIndex(listView, "Menu.MusicLibrary.AllSongs");
            if (insertIndex < 0) 
                return;

            var allSongs = entries.Select(e => e.song).ToArray();
            listView.Insert(insertIndex++, new CategoryViewType("ARCHIPELAGO", allSongs.Length, allSongs, menu.RefreshAndReselect));

            var AllActionItems = container.GetAllAquiredActionItems();
            //var AllActionItems = container.ApItemsRecieved;
            var SwapSongs = AllActionItems.Where(x => x.Type == StaticItems.SwapPick && !container.seedConfig.ApItemsUsed.Contains(x));
            var SwapSongRand = AllActionItems.Where(x => x.Type == StaticItems.SwapRandom && !container.seedConfig.ApItemsUsed.Contains(x));
            var LowerDifficulty = AllActionItems.Where(x => x.Type == StaticItems.LowerDifficulty && !container.seedConfig.ApItemsUsed.Contains(x));

            if (container.SlotData.SetlistNeededForGoal > 0)
            {
                var current = container.ApItemsRecieved.Count(x => x.Type == StaticItems.SongCompletion);
                listView.Insert(insertIndex++, new CategoryViewType($"- SETLIST GOAL {current}/{container.SlotData.SetlistNeededForGoal}", current, new SongEntry[0], menu.RefreshAndReselect));
            }
            if (container.SlotData.FamePointsForGoal > 0)
            {
                var current = container.ApItemsRecieved.Count(x => x.Type == StaticItems.FamePoint);
                listView.Insert(insertIndex++, new CategoryViewType($"- FAME GOAL {current}/{container.SlotData.FamePointsForGoal}", current, new SongEntry[0], menu.RefreshAndReselect));
            }
            if (container.GoalItemInPool(out var GoalItemRecieved, out var recieveInfo))
                listView.Insert(insertIndex++, new CategoryViewType($"- FOUND GOAL ITEM [{GoalItemRecieved}]", GoalItemRecieved ? 1 : 0, new SongEntry[0], () => ShowGoalRecieveMessage(container, GoalItemRecieved, recieveInfo)));

            if (SwapSongs.Any() && allSongs.Any())
                listView.Insert(insertIndex++, new CategoryViewType($"- USE SWAP SONG (Pick)", SwapSongs.Count(), new SongEntry[0], () => SwapSongMenu.ShowMenu(container, SwapSongs.First())));

            if (SwapSongRand.Any() && allSongs.Any())
                listView.Insert(insertIndex++, new CategoryViewType($"- USE SWAP SONG (Random)", SwapSongRand.Count(), new SongEntry[0], () => SwapSongMenu.ShowMenu(container, SwapSongRand.First())));

            if (LowerDifficulty.Any() && allSongs.Any())
                listView.Insert(insertIndex++, new CategoryViewType($"- USE LOWER DIFFICULTY", LowerDifficulty.Count(), new SongEntry[0], () => LowerDifficultyMenu.ShowMenu(container, LowerDifficulty.First())));

            if (container.seedConfig.EnergyLinkMode > EnergyLinkType.disabled || true)
                listView.Insert(insertIndex++, new CategoryViewType($"- OPEN ENERGY LINK SHOP", (int)container.seedConfig.EnergyLinkMode, new SongEntry[0], () => EnergyLinkShop.ShowMenu(container)));

            if (container.SlotData.GoalData.IsSongUnlocked(container) && container.SlotData.GoalData.HadYargSongEntry(container, out var GoalSong) && container.SlotData.GoalData.HasAvailableLocations(container))
            {
                var Pool = container.SlotData.GoalData.PoolName;
                listView.Insert(insertIndex++, new CategoryViewType($"GOAL SONG: {Pool.ToUpper()}", 1, new SongEntry[] { GoalSong }, () => ShowPoolData(container, Pool)));
                listView.Insert(insertIndex++, new SongViewType(menu, GoalSong));
            }

            foreach (var pool in entries
                .OrderBy(e => e.APData.GetPool(container.SlotData).instrument.GetDescription(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.APData.PoolName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(e => e.APData.PoolName))
            {
                string PoolName = pool.Key.ToUpper();
                if (container.seedConfig.ShowMissingInstruments && !container.ReceivedInstruments.ContainsKey(container.SlotData.Pools[pool.Key].instrument))
                    PoolName = $"<color=#FF4040>{PoolName}</color>";

                var poolSongs = pool.Select(e => e.song).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                listView.Insert(insertIndex++, new CategoryViewType($"AP: {PoolName}", poolSongs.Length, poolSongs, () => ShowPoolData(container, pool.Key)));

                foreach (var song in poolSongs)
                    listView.Insert(insertIndex++, new SongViewType(menu, song));
            }
        }

        public static void ShowGoalRecieveMessage(APConnectionContainer container, bool Recieved, BaseYargAPItem recieveInfo)
        {
            if (!Recieved)
            {
                ToastManager.ToastError($"Your goal song unlock item has not been found!");
                return;
            }
            var Team = container.GetSession().Players.ActivePlayer.Team;
            var Player = container.GetSession().Players.GetPlayerInfo(Team, recieveInfo.SendingPlayerSlot);
            var LocationInfo = container.GetSession().Locations.GetLocationNameFromId(recieveInfo.SendingPlayerLocation, recieveInfo.SendingPlayerGame);
            DialogManager.Instance.ShowMessage("Goal Unlock Item Found!", $"Found by Player:\n{Player.Name}\n\nFrom Location:\n{LocationInfo}\n\nPlaying Game:\n{Player.Game}");
        }

        /// <summary>
        /// Creates an invisible blocking dialog used to prevent UI interaction while custom BepInEx menus are displayed.
        /// Must be closed manually.
        /// </summary>
        public static MessageDialog ShowBlockerDialog()
        {
            var dialog = DialogManager.Instance.ShowMessage("", "");
            dialog.ClearButtons();

            foreach (var graphic in dialog.GetComponentsInChildren<Component>())
            {   // Keep the "Tint" image, thats what actually blocks the UI 
                if (graphic.GetType().Name == "Image" && graphic.gameObject.name != "Tint")
                {
                    var enabled = graphic.GetType().GetProperty("enabled");
                    enabled?.SetValue(graphic, false);
                }
            }

            return dialog;
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
                .AppendLine($"REQUIRED INSTRUMENT:")
                .AppendLine($"{SongPool.instrument.GetDescription()}")
                .AppendLine()
                .AppendLine($"REWARD 1 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {SongPool.completion_requirements.reward1_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {SongPool.completion_requirements.reward1_req.GetDescription()}")
                .AppendLine()
                .AppendLine($"REWARD 2 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {SongPool.completion_requirements.reward2_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {SongPool.completion_requirements.reward2_req.GetDescription()}");
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
                ToastManager.ToastInformation($"DeathLink Received!\n\n{deathLink.Source} {deathLink.Cause}");
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
                var field = AccessTools.Field(typeof(GameManager), "_pauseMenu");
                object pauseMenuObj = field.GetValue(current);
                if (pauseMenuObj is PauseMenuManager pm && !current.IsPractice && !MonoSingleton<DialogManager>.Instance.IsDialogShowing)
                {
                    //TODO: This works but YARG spits out a bunch of errors. I thinks it because I don't give the pause menu enough time to load before restarting.
                    if (!pm.IsOpen)
                        current.Pause(true);
                    if (pm.IsOpen)
                        pm.Restart();
                }
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
        private static MethodInfo _getAverageHappinessMethod;
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
        /// Gets the average happiness across all players in the engine manager.
        /// </summary>
        public static float GetAverageHappiness(this EngineManager engineManager)
        {
            if (_getAverageHappinessMethod == null) 
                _getAverageHappinessMethod = typeof(EngineManager).GetMethod("GetAverageHappiness", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)_getAverageHappinessMethod?.Invoke(engineManager, null);
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

            while (engineManager.GetAverageHappiness() < TARGET_HAPPINESS)
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
        
    }
}
