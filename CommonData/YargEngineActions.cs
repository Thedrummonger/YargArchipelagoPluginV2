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
                var poolSongs = pool.Select(e => e.song).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                listView.Insert(insertIndex++, new CategoryViewType($"AP: {pool.Key.ToUpper()}", poolSongs.Length, poolSongs, () => ShowPoolData(container, pool.Key)));

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
        /// A special MessageDialog with no gui elements. Used to block the ui while custom Bepin menus are being displayed. Must be closed manually. 
        /// </summary>
        /// <returns></returns>
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

        public static void ApplyStarPowerItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            handler.logger.LogInfo($"Gaining Star Power");
            MethodInfo method = AccessTools.Method(typeof(BaseEngine), "GainStarPower");
            foreach (var player in current.Players)
                method.Invoke(player.BaseEngine, new object[] { player.BaseEngine.TicksPerQuarterSpBar });

        }
        public static void ApplyRockMetertrapItem(APConnectionContainer handler)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
#if NIGHTLY
            handler.logger.LogInfo($"Reducing Rock Meter");
            foreach (var player in current.Players)
                AddHappiness(player.GetEngineContainer(), -0.25f);
#else
            GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
#endif

        }

        public static void ApplyDeathLink(APConnectionContainer handler, DeathLink deathLink)
        {
            if (!handler.IsInSong(out var current, out _))
                return;
            try
            {
                handler.logger.LogInfo($"Applying Death Link");
#if NIGHTLY
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
#else
                ForceExitSong(handler);
#endif
                ToastManager.ToastInformation($"DeathLink Received!\n\n{deathLink.Source} {deathLink.Cause}");
            }
            catch (Exception e)
            {
                handler.logger.LogError($"Failed to apply deathlink\n{e}");
            }
        }

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

#if NIGHTLY

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

        public static void SetBandHappiness(APConnectionContainer handler, float? delta = null)
        {
            if (!handler.IsInSong(out var gameManager, out _) || gameManager.IsPractice)
                return;
            foreach (var player in gameManager.Players)
            {
                var EngineContainer = player.GetEngineContainer();
                EngineContainer.SetHappiness(gameManager.EngineManager, delta ?? EngineContainer.RockMeterPreset.StartingHappiness);
            }
        }

        private static MethodInfo _addHappinessMethod;
        private static PropertyInfo _happinessProperty;
        private static MethodInfo _updateHappinessMethod;
        private static FieldInfo _engineContainerField;
        public static EngineManager.EngineContainer GetEngineContainer(this BasePlayer player)
        {
            if (_engineContainerField == null)
                _engineContainerField = typeof(BasePlayer).GetField("EngineContainer", BindingFlags.NonPublic | BindingFlags.Instance);

            return (EngineManager.EngineContainer)_engineContainerField.GetValue(player);
        }

        public static void AddHappiness(this EngineManager.EngineContainer container, float delta)
        {
            if (_addHappinessMethod == null)
                _addHappinessMethod = typeof(EngineManager.EngineContainer).GetMethod("AddHappiness", BindingFlags.NonPublic | BindingFlags.Instance);

            _addHappinessMethod?.Invoke(container, new object[] { delta });
        }

        public static void SetHappiness(this EngineManager.EngineContainer container, EngineManager engineManager, float value)
        {
            if (_happinessProperty == null)
                _happinessProperty = typeof(EngineManager.EngineContainer).GetProperty("Happiness", BindingFlags.Public | BindingFlags.Instance);

            if (_updateHappinessMethod == null)
                _updateHappinessMethod = typeof(EngineManager).GetMethod("UpdateHappiness", BindingFlags.NonPublic | BindingFlags.Instance);

            value = Mathf.Clamp(value, -3f, 1f);

            _happinessProperty?.SetValue(container, value);

            _updateHappinessMethod?.Invoke(engineManager, null);
        }
#endif

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
