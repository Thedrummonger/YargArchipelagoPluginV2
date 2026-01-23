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
using YARG.Gameplay;
using YARG.Gameplay.HUD;
//----------------------------------------------------
using YARG.Gameplay.Player;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YargArchipelagoCommon;

namespace YargArchipelagoPlugin
{
    public static class YargEngineActions
    {
        public static void ApplyActionItem(ArchipelagoService APHandler, CommonData.ActionItemData ActionItem)
        {
            APHandler.Log($"Applying Action Item {ActionItem.type}");
            if (!APHandler.IsInSong() || APHandler.GetCurrentSong().IsPractice)
            {
                APHandler.Log($"Exiting, not in Song");
                return;
            }

            switch (ActionItem.type)
            {
                case CommonData.APActionItem.RockMeterTrap:
                    ToastManager.ToastInformation($"{ActionItem.Sender} sent you a Rock Meter Trap!");
                    ApplyRockMetertrapItem(APHandler);
                    break;
                case CommonData.APActionItem.Restart:
                    ToastManager.ToastInformation($"{ActionItem.Sender} sent you a Restart Trap!");
                    ForceRestartSong(APHandler);
                    break;
                case CommonData.APActionItem.StarPower:
                    ToastManager.ToastInformation($"{ActionItem.Sender} sent you a some Star Power!");
                    ApplyStarPowerItem(APHandler);
                    break;
            }
        }
        public static void ApplyStarPowerItem(ArchipelagoService handler)
        {
            if (!handler.IsInSong())
                return;
            handler.Log($"Gaining Star Power");
            MethodInfo method = AccessTools.Method(typeof(BaseEngine), "GainStarPower");
            foreach (var player in handler.GetCurrentSong().Players)
                method.Invoke(player.BaseEngine, new object[] { player.BaseEngine.TicksPerQuarterSpBar });

        }
        public static void ApplyRockMetertrapItem(ArchipelagoService handler)
        {
            if (!handler.IsInSong())
                return;
#if NIGHTLY
            handler.Log($"Reducing Rock Meter");
            foreach (var player in handler.GetCurrentSong().Players)
                AddHappiness(player.GetEngineContainer(), -0.25f);
#else
            GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
#endif

        }

        public static void ApplyDeathLink(ArchipelagoService handler, CommonData.DeathLinkData deathLinkData)
        {
            if (!handler.IsInSong())
                return;
            try
            {
                handler.Log($"Applying Death Link");
#if STABLE
                ForceExitSong(handler);
#else
                switch (deathLinkData.Type)
                {
                    case CommonData.DeathLinkType.RockMeter:
                        SetBandHappiness(handler, 0.02f);
                        break;
                    case CommonData.DeathLinkType.Fail:
                        ForceFailSong(handler);
                        break;
                }
#endif
                ToastManager.ToastInformation($"DeathLink Received!\n\n{deathLinkData.Source} {deathLinkData.Cause}");
                //DialogManager.Instance.ShowMessage("DeathLink Received!", $"{deathLinkData.Source} {deathLinkData.Cause}");
            }
            catch (Exception e)
            {
                handler.Log($"Failed to apply deathlink\n{e}");
            }
        }

        private static void ForceRestartSong(ArchipelagoService handler)
        {
            if (!handler.IsInSong()) 
                return;
            try
            {
                var gm = handler.GetCurrentSong();
                var field = AccessTools.Field(typeof(GameManager), "_pauseMenu");
                object pauseMenuObj = field.GetValue(gm);
                if (pauseMenuObj is PauseMenuManager pm && !gm.IsPractice && !MonoSingleton<DialogManager>.Instance.IsDialogShowing)
                {
                    //TODO: This works but YARG spits out a bunch of errors. I thinks it because I don't give the pause menu enough time to load before restarting.
                    if (!pm.IsOpen)
                        gm.Pause(true);
                    if (pm.IsOpen)
                        pm.Restart();
                }
            }
            catch (Exception e)
            {
                handler.Log($"Failed to force restart song\n{e}");
            }
        }

#if NIGHTLY

        public static async void ForceFailSong(ArchipelagoService handler)
        {
            var gameManager = handler.GetCurrentSong();
            if (!handler.IsInSong() || gameManager.IsPractice)
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
        
        public static void SetBandHappiness(ArchipelagoService handler, float? delta = null)
        {
            var gameManager = handler.GetCurrentSong();
            if (!handler.IsInSong() || gameManager.IsPractice)
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

        private static void ForceExitSong(ArchipelagoService handler)
        {
            if (!handler.IsInSong())
                return;
            try
            {
                handler.Log($"Forcing Quit");
                handler.GetCurrentSong().ForceQuitSong();
            }
            catch (Exception e)
            {
                handler.Log($"Failed to force exit song\n{e}");
            }
        }
        public static bool UpdateRecommendedSongsMenu()
        {
            var Menu = UnityEngine.Object.FindObjectOfType<MusicLibraryMenu>();
            if (Menu == null || !Menu.gameObject.activeInHierarchy)
                return false;

            Menu.RefreshAndReselect();
            return true;
        }
        public static void DumpAvailableSongs(SongCache SongCache, ArchipelagoService handler)
        {
            Dictionary<string, CommonData.SongData> SongData = new Dictionary<string, CommonData.SongData>();

            foreach (var instrument in SongCache.Instruments)
            {
                if (!YargAPUtils.IsSupportedInstrument(instrument.Key, out var supportedInstrument))
                    continue;
                foreach (var Difficulty in instrument.Value)
                {
                    if (Difficulty.Key < 0)
                        continue;
                    foreach (var song in Difficulty.Value)
                    {
                        var data = YargAPUtils.ToSongData(song);
                        if (!SongData.ContainsKey(data.SongChecksum))
                            SongData[data.SongChecksum] = data;
                        SongData[data.SongChecksum].Difficulties[supportedInstrument.Value] = Difficulty.Key;
                    }
                }
            }
            handler.Log($"Dumping Info for {SongData.Values.Count} songs");
            if (!Directory.Exists(CommonData.DataFolder)) Directory.CreateDirectory(CommonData.DataFolder);
            File.WriteAllText(CommonData.SongExportFile, JsonConvert.SerializeObject(SongData.Values.ToArray(), Formatting.Indented));
        }
    }
}
