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
            Dictionary<string, SongExportData> SongData = new Dictionary<string, SongExportData>();

            foreach (var instrument in SongCache.Instruments)
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
                    }
                }
            }
            if (!Directory.Exists(CommonData.DataFolder)) Directory.CreateDirectory(CommonData.DataFolder);
            File.WriteAllText(CommonData.SongExportFile, JsonConvert.SerializeObject(SongData.Values.ToArray(), Formatting.Indented));
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

        public static void InsertAPListViewSongs(MusicLibraryMenu menu, List<ViewType> listView, IEnumerable<(SongEntry song, string Instrument)> entries)
        {
            int insertIndex = GetListViewIndex(listView, "Menu.MusicLibrary.AllSongs");
            var groups = entries.GroupBy(t => t.Instrument, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in groups)
            {
                var songs = grp.Select(t => t.song).OrderBy<SongEntry, string>(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                string header = $"Archipelago Songs: {grp.Key}".ToUpper();
                listView.Insert(insertIndex++, new CategoryViewType(header, songs.Length, songs, menu.RefreshAndReselect));

                foreach (var song in songs)
                    listView.Insert(insertIndex++, new SongViewType(menu, song));
            }
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
#if STABLE
                ForceExitSong(handler);
#else
                switch (handler.seedConfig.DeathLinkMode)
                {
                    case CommonData.DeathLinkType.RockMeter:
                        SetBandHappiness(handler, 0.02f);
                        break;
                    case CommonData.DeathLinkType.Fail:
                        ForceFailSong(handler);
                        break;
                    default:
                        return;
                }
#endif
                ToastManager.ToastInformation($"DeathLink Received!\n\n{deathLinkData.Source} {deathLinkData.Cause}");
                //DialogManager.Instance.ShowMessage("DeathLink Received!", $"{deathLinkData.Source} {deathLinkData.Cause}");
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
