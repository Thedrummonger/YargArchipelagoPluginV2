using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;
using YargArchipelagoCommon;

namespace YargArchipelagoPlugin
{
    [HarmonyPatch]
    public static class APPatches
    {
        public static ArchipelagoEventManager EventManager;


        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        public static void GameManager_Awake(GameManager __instance)
        {

        }

        [HarmonyPatch(typeof(GameManager), "OnDestroy")]
        [HarmonyPrefix]
        public static void GameManager_OnDestroy()
        {

        }


        [HarmonyPatch(typeof(GameManager), "RecordScores")]
        [HarmonyPostfix]
        public static void GameManager_RecordScores_Postfix(GameManager __instance, ReplayInfo replayInfo)
        {

        }

#if NIGHTLY 
        [HarmonyPatch(typeof(GameManager), "OnSongFailed")]
        [HarmonyPrefix]
        public static void GameManager_OnSongFailed(GameManager __instance)
        {

        }
#endif

        [HarmonyPatch(typeof(SongContainer), "FillContainers")]
        [HarmonyPostfix]
        public static void SongContainer_FillContainers(SongCache ____songCache) => YargEngineActions.DumpAvailableSongs(____songCache);


        [HarmonyPatch(typeof(MusicLibraryMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_OnEnable(MusicLibraryMenu __instance)
        {

        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "CreateNormalViewList")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_CreateNormalViewList_Postfix(MusicLibraryMenu __instance, List<ViewType> __result)
        {
            var SongEntries = new (SongEntry, string)[0];
            YargEngineActions.InsertAPListViewSongs(__instance, __result, SongEntries);
            var calc = AccessTools.Method(typeof(MusicLibraryMenu), "CalculateCategoryHeaderIndices");
            calc.Invoke(__instance, new object[] { __result });
        }

    }
}
