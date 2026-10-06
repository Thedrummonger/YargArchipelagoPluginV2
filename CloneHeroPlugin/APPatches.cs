using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using YargArchipelagoCommon;
using UnityEngine;

namespace YargArchipelagoCommon
{
    [HarmonyPatch]
    public static class APPatches
    {

        public static void InsertAPSongs(SongSelect menu, Il2CppSystem.Collections.Generic.List<SongSection> sections) =>
            EngineActions.InsertAPListViewSongs(ArchipelagoPlugin.APcontainer, menu, sections);

        public static event Action<GameManager> OnSongStarted;
        public static event Action OnSongEnded;
        public static event Action<GameManager> OnGameManagerUpdateThrottled;
        public static event Action<APSongResult> OnRecordScore;
        public static event Action OnSongContainersUpdated;
        public static event Action<SongSelect, Il2CppSystem.Collections.Generic.List<SongSection>> OnCreateNormalView;

        public static bool HasAvailableAPSongUpdate = false;
        public static bool IgnoreScoreForNextSong = false;
        public static bool IgnoreDeathLinkForCurrentSong = false;
        public static bool BlockMainMenuInput = false;
        private static float _nextTrapCheckTime;
        private static bool _initializedMenu;
        private static bool _openedForSelection;

        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        public static void GameManager_Awake(GameManager __instance)
        {
            _nextTrapCheckTime = 0f;
            IgnoreDeathLinkForCurrentSong = false;
            OnSongStarted?.Invoke(__instance);
        }

        [HarmonyPatch(typeof(GameManager), "OnDestroy")]
        [HarmonyPrefix]
        public static void GameManager_OnDestroy() => OnSongEnded?.Invoke();

        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        public static void GameManager_Update_Postfix(GameManager __instance)
        {
            ArchipelagoPlugin.Tick();
            if (Time.unscaledTime >= _nextTrapCheckTime)
            {
                _nextTrapCheckTime = Time.unscaledTime + 0.2f;
                OnGameManagerUpdateThrottled?.Invoke(__instance);
            }
            var Modifiers = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            if (!Modifiers || !__instance.songLoaded || __instance.isSongOver) return;

            if (Input.GetKeyDown(KeyCode.Q))
            {
                __instance.songTime = __instance.songLength + 3.0;
                IgnoreScoreForNextSong = true;
                CloneHeroMappings.EndSong(__instance);
            }
            if (Input.GetKeyDown(KeyCode.R))
                EngineActions.ForceRestartSong(ArchipelagoPlugin.APcontainer);
            if (Input.GetKeyDown(KeyCode.S))
                EngineActions.ApplyStarPowerItem(ArchipelagoPlugin.APcontainer);
            if (Input.GetKeyDown(KeyCode.M))
                EngineActions.ApplyRockMetertrapItem(ArchipelagoPlugin.APcontainer);
            if (Input.GetKeyDown(KeyCode.D))
                ArchipelagoEventManager.ApplyDeathLink(ArchipelagoPlugin.APcontainer, null);
        }

        [HarmonyPatch]
        private static class RockMeter_Updated_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.RockMeterUpdated;

            [HarmonyPrefix]
            private static bool Prefix(BasePlayer __instance, float __0) =>
                EngineActions.TryPreventSongFail(__instance, __0);
        }

        [HarmonyPatch(typeof(EndOfSong), "Start")]
        [HarmonyPostfix]
        public static void EndOfSong_Start_Postfix(EndOfSong __instance)
        {
            if (OnRecordScore == null)
                return;
            try
            {
                var result = EngineActions.GetSongResults(__instance);
                if (result != null)
                    OnRecordScore.Invoke(result);
                else
                    ArchipelagoPlugin.PluginLog?.LogWarning("Could not evaluate Clone Hero song result. song or player stats were unavailable.");
            }
            catch (Exception e)
            {
                ArchipelagoPlugin.PluginLog?.LogError($"Could not evaluate Clone Hero song result\n{e}");
            }
        }

        [HarmonyPatch(typeof(EndOfSong), "Update")]
        [HarmonyPrefix]
        public static bool EndOfSong_Update_Prefix()
        {
            ArchipelagoPlugin.Tick();
            return !ArchipelagoConnectionDialog.BlocksMenuInput;
        }

        [HarmonyPatch(typeof(SongScanEnumerator), nameof(SongScanEnumerator.MoveNext))]
        private static class SongScan_Completed_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(SongScanEnumerator __instance, out bool __state)
            {
                var scan = CloneHeroMappings.GetSongScan(__instance);
                __state = scan != null && scan.isScanning;
            }

            [HarmonyPostfix]
            private static void Postfix(SongScanEnumerator __instance, bool __result, bool __state)
            {
                if (!__state || __result || CloneHeroMappings.GetSongScan(__instance).isScanning)
                    return;
                try
                {
                    EngineActions.UpdateAvailableSongs();
                    EngineActions.DumpAvailableSongs();
                    HasAvailableAPSongUpdate = true;
                    OnSongContainersUpdated?.Invoke();
                }
                catch (Exception e)
                {
                    ArchipelagoPlugin.PluginLog?.LogError($"Failed to update the completed Clone Hero song library: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(SongSelect), "OnEnable")]
        [HarmonyPrefix]
        public static void SongSelect_OnEnable(SongSelect __instance)
        {
            ArchipelagoPlugin.Tick();
            CreateNormalView(__instance);
        }

        [HarmonyPatch(typeof(SongSelect), "Update")]
        [HarmonyPrefix]
        public static bool SongSelect_Update(SongSelect __instance)
        {
            ArchipelagoPlugin.Tick();
            if (HasAvailableAPSongUpdate && CreateNormalView(__instance))
                EngineActions.RefreshSongLibrary(__instance);
            return !ArchipelagoConnectionDialog.BlocksMenuInput;
        }

        [HarmonyPatch(typeof(BaseMenu), "Update")]
        [HarmonyPrefix]
        public static bool BaseMenu_Update()
        {
            ArchipelagoPlugin.Tick();
            return !ArchipelagoConnectionDialog.BlocksMenuInput;
        }

        [HarmonyPatch]
        private static class SongLibrary_Reindex_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.SongSectionsReindex;

            [HarmonyPostfix]
            private static void Postfix(Il2CppSystem.Collections.Generic.List<SongSection> __0) =>
                EngineActions.OnSongSectionsRebuilt(__0);
        }

        private static bool CreateNormalView(SongSelect menu)
        {
            var sections = CloneHeroMappings.GetSongSections();
            if (sections == null)
                return false;

            HasAvailableAPSongUpdate = false;
            EngineActions.RemoveAPListViewSongs(menu, sections);
            OnCreateNormalView?.Invoke(menu, sections);
            EngineActions.ReindexSongSections(sections);
            return true;
        }

        [HarmonyPatch]
        private static class SongSelect_Render_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.SongListRender;

            [HarmonyPostfix]
            private static void Postfix(SongSelect __instance) =>
                FormHelpers.MarkHintedSongs(ArchipelagoPlugin.APcontainer, __instance);
        }

        [HarmonyPatch(typeof(MainMenuTipSwitcher), "Start")]
        [HarmonyPrefix]
        public static void MainMenuTipSwitcher_Start(MainMenuTipSwitcher __instance)
        {
            const string leaderboardPrompt = "%holdEnterLeaderboardsMode%";
            __instance.standardNavigationTip = __instance.standardNavigationTip?.Replace(leaderboardPrompt, "Leaderboard Disabled in AP");
            __instance.noMusicNavigationTip = __instance.noMusicNavigationTip?.Replace(leaderboardPrompt, "Leaderboard Disabled in AP");
        }

        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        public static void AddAPButton(MainMenu __instance)
        {
            try
            {
                _openedForSelection = false;
                if (!EngineActions.TrySetArchipelagoMenuEntry(__instance))
                    return;
                ArchipelagoConnectionDialog.Initialize(__instance.textObjects[EngineActions.ArchipelagoMenuIndex]);
                if (!_initializedMenu)
                {
                    _initializedMenu = true;
                    ArchipelagoConnectionDialog.Visible = ArchipelagoPlugin.ShowConnectionDialogOnStartup.Value;
                }
            }
            catch (Exception e) { ArchipelagoPlugin.PluginLog.LogError($"Could not create the connection dialog: {e}"); }
        }

        public static void HandleMenuInput(MainMenu menu)
        {
            bool wasVisible = ArchipelagoConnectionDialog.Visible;
            ArchipelagoConnectionDialog.Tick();
            if (menu.GetSelectedMenuIndex() != EngineActions.ArchipelagoMenuIndex)
                _openedForSelection = false;
            bool clicked = !wasVisible && !ArchipelagoConnectionDialog.Visible && !ArchipelagoConnectionDialog.BlocksMenuInput &&
                ArchipelagoConnectionDialog.IsInitialized && EngineActions.IsArchipelagoMenuClicked(menu);
            if (clicked)
            {
                ArchipelagoConnectionDialog.Visible = true;
                _openedForSelection = true;
            }
            BlockMainMenuInput = wasVisible || clicked || ArchipelagoConnectionDialog.BlocksMenuInput;
        }

        public static void HandleMenuSelection(MainMenu menu)
        {
            if (!ArchipelagoConnectionDialog.IsInitialized || menu.GetSelectedMenuIndex() != EngineActions.ArchipelagoMenuIndex || _openedForSelection)
                return;
            _openedForSelection = true;
            ArchipelagoConnectionDialog.Visible = true;
        }

        [HarmonyPatch(typeof(MainMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MainMenu_OnEnable(MainMenu __instance) => AddAPButton(__instance);

        [HarmonyPatch(typeof(MainMenu), "Update")]
        [HarmonyPrefix]
        public static bool MainMenu_Update(MainMenu __instance)
        {
            ArchipelagoPlugin.Tick();
            HandleMenuInput(__instance);
            return !BlockMainMenuInput;
        }

        [HarmonyPatch(typeof(MainMenu), "Update")]
        [HarmonyPostfix]
        public static void MainMenu_Update_Postfix(MainMenu __instance) =>
            EngineActions.RestoreArchipelagoMenuLabel(__instance);

        [HarmonyPatch]
        private static class MainMenu_Select_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.MainMenuSelection;

            [HarmonyPostfix]
            private static void Postfix(MainMenu __instance) => HandleMenuSelection(__instance);
        }

        [HarmonyPatch]
        private static class MainMenu_LeaderboardToggle_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.MainMenuLeaderboardToggle;

            [HarmonyPrefix]
            private static bool Prefix()
            {
                APToastManager.Enqueue("Archipelago", "Leaderboard Disabled in Archipelago");
                return false;
            }
        }

        [HarmonyPatch]
        private static class Leaderboards_GetMode_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.GetLeaderboardMode;

            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        [HarmonyPatch]
        private static class Leaderboards_SetMode_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => CloneHeroMappings.SetLeaderboardMode;

            [HarmonyPrefix]
            private static void Prefix(ref bool __0) => __0 = false;
        }

    }
}
