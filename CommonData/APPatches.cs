using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Localization;
using YARG.Menu.Main;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Playback;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;
using YargArchipelagoCommon;

namespace YargArchipelagoPlugin
{
    [HarmonyPatch]
    public static class APPatches
    {
        public static event Action<MusicLibraryMenu, List<ViewType>> OnCreateNormalView;
        public static event Action<GameManager> OnSongStarted;
        public static event Action OnSongEnded;
        public static event Action<GameManager> OnRecordScore;
        public static event Action<GameManager> OnSongFail;
        public static bool HasAvailableAPSongUpdate = false;
        public static bool IgnoreScoreForNextSong = false;
        private static bool FirstAwake = true;

        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        public static void MainMenu_Start(MainMenu __instance)
        {
            if (FirstAwake)
            {
                ArchipelagoPlugin.ToggleArchipelagoDialog();
                FirstAwake = false;
            }
        }

        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        public static void GameManager_Awake(GameManager __instance) => OnSongStarted?.Invoke(__instance);

        [HarmonyPatch(typeof(GameManager), "OnDestroy")]
        [HarmonyPrefix]
        public static void GameManager_OnDestroy() => OnSongEnded?.Invoke();


        [HarmonyPatch(typeof(GameManager), "RecordScores")]
        [HarmonyPostfix]
        public static void GameManager_RecordScores_Postfix(GameManager __instance, ReplayInfo replayInfo) =>
            OnRecordScore?.Invoke(__instance);

#if NIGHTLY 
        [HarmonyPatch(typeof(GameManager), "OnSongFailed")]
        [HarmonyPrefix]
        public static void GameManager_OnSongFailed(GameManager __instance) => OnSongFail?.Invoke(__instance);
#endif

        [HarmonyPatch(typeof(SongContainer), "FillContainers")]
        [HarmonyPostfix]
        public static void SongContainer_FillContainers(SongCache ____songCache) => YargEngineActions.DumpAvailableSongs(____songCache);


        [HarmonyPatch(typeof(MusicLibraryMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_OnEnable(MusicLibraryMenu __instance)
        {
            if (HasAvailableAPSongUpdate)
                HasAvailableAPSongUpdate = !YargEngineActions.UpdateRecommendedSongsMenu();
        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "CreateNormalViewList")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_CreateNormalViewList_Postfix(MusicLibraryMenu __instance, List<ViewType> __result)
        {
            OnCreateNormalView?.Invoke(__instance, __result);
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        public static void GameManager_Update_Postfix(GameManager __instance)
        {
            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.qKey.wasPressedThisFrame)
            {
                var songRunner = AccessTools.Field(typeof(GameManager), "_songRunner").GetValue(__instance) as SongRunner;
                songRunner.SetSongTime(__instance.SongLength + 3.0, 0.0);

                var endSongMethod = typeof(GameManager).GetMethod("EndSong", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                IgnoreScoreForNextSong = true;
                endSongMethod?.Invoke(__instance, new object[] { });
            }
        }
        [HarmonyPatch(typeof(DevWatermark), "Start")]
        [HarmonyPostfix]
        public static void DevWatermark_Start_Postfix(DevWatermark __instance)
        {
            var field = typeof(DevWatermark).GetField("_watermarkText", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null) return;
            var watermarkText = field.GetValue(__instance);
            if (watermarkText == null) return;
            var textProperty = watermarkText.GetType().GetProperty("text");
            if (textProperty == null) return;

            string currentText = textProperty.GetValue(watermarkText) as string;

            if (!string.IsNullOrEmpty(currentText))
                textProperty.SetValue(watermarkText, $"{currentText}\nYarg Archipelago plugin V{ArchipelagoPlugin.pluginVersion}. Press F10 to connect!");
        }
    }
}
