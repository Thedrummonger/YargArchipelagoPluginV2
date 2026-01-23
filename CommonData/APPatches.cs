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
        public static void GameManager_Awake(GameManager __instance) => EventManager.SongStarted(__instance);

        [HarmonyPatch(typeof(GameManager), "OnDestroy")]
        [HarmonyPrefix]
        public static void GameManager_OnDestroy() => EventManager.SongEnded();


        [HarmonyPatch(typeof(GameManager), "RecordScores")]
        [HarmonyPostfix]
        public static void GameManager_RecordScores_Postfix(GameManager __instance, ReplayInfo replayInfo)
        {
            EventManager?.APHandler?.Log($"Passed Song {__instance.Song.Name}: {__instance.Song.Album} [{__instance.Song.Hash}]");
            var songPassInfo = new CommonData.SongCompletedData(__instance.Song.ToSongData(), true, YargAPUtils.CreatePlayerScores(__instance), __instance.BandScore);
            EventManager.SendSongCompletionResults(songPassInfo);
        }

#if NIGHTLY 
        [HarmonyPatch(typeof(GameManager), "OnSongFailed")]
        [HarmonyPrefix]
        public static void GameManager_OnSongFailed(GameManager __instance)
        {
            if (!SettingsManager.Settings.NoFailMode.Value && !__instance.IsPractice && !__instance.PlayerHasFailed && __instance?.Song != null)
            {
                EventManager?.APHandler?.Log($"Failed Song {__instance.Song.Name}: {__instance.Song.Album} [{__instance.Song.Hash}]");
                var songPassInfo = new CommonData.SongCompletedData(__instance.Song.ToSongData(), false, YargAPUtils.CreatePlayerScores(__instance), __instance.BandScore);
                EventManager.SendSongCompletionResults(songPassInfo);
            }
        }
#endif

        [HarmonyPatch(typeof(SongContainer), "FillContainers")]
        [HarmonyPostfix]
        public static void SongContainer_FillContainers(SongCache ____songCache)
            => YargEngineActions.DumpAvailableSongs(____songCache, EventManager.APHandler);


        [HarmonyPatch(typeof(MusicLibraryMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_OnEnable(MusicLibraryMenu __instance)
        {
            if (EventManager.APHandler.HasAvailableSongUpdate)
                YargEngineActions.UpdateRecommendedSongsMenu();
        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "CreateNormalViewList")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_CreateNormalViewList_Postfix(MusicLibraryMenu __instance, List<ViewType> __result)
        {
            string allSongsKey = Localize.Key("Menu.MusicLibrary.AllSongs");
            var primaryField = AccessTools.Field(typeof(CategoryViewType), "_primary");
            int insertIndex = -1;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] is CategoryViewType cat && (string)primaryField.GetValue(cat) == allSongsKey)
                {
                    insertIndex = i;
                    break;
                }
            }
            if (insertIndex < 0)
                return;

            var entries = EventManager.APHandler.GetAvailableSongs();
            var groups = entries.GroupBy(t => t.ProfileName, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in groups)
            {
                var songs = grp.Select(t => t.song).OrderBy<SongEntry, string>(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                string header = $"Archipelago Setlist ({grp.Key})".ToUpper();
                __result.Insert(insertIndex++, new CategoryViewType(header, songs.Length, songs, __instance.RefreshAndReselect));

                foreach (var song in songs)
                    __result.Insert(insertIndex++, new SongViewType(__instance, song));
            }
        }

    }
}
