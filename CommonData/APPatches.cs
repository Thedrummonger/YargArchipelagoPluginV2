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
using YARG.Menu.Navigation;
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
        public static event Action<GameManager> OnGameManagerUpdateThrottled;
        public static event Action<GameManager> OnRecordScore;
        public static event Action<GameManager> OnSongFail;
        public static event Action<EngineManager> OnUpdateHappiness;
        public static event Action OnSongContainersUpdated;
        public static bool HasAvailableAPSongUpdate = false;
        public static bool IgnoreScoreForNextSong = false;
        private static bool FirstAwake = true;

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

        [HarmonyPatch(typeof(GameManager), "OnSongFailed")]
        [HarmonyPrefix]
        public static void GameManager_OnSongFailed(GameManager __instance) => OnSongFail?.Invoke(__instance);

        [HarmonyPatch(typeof(EngineManager), "UpdateHappiness", MethodType.Normal)]
        [HarmonyPrefix]
        public static void EngineManager_UpdateHappiness(EngineManager __instance) => OnUpdateHappiness?.Invoke(__instance);

        [HarmonyPatch(typeof(SongContainer), "FillContainers")]
        [HarmonyPostfix]
        public static void SongContainer_FillContainers()
        {
            YargEngineActions.DumpAvailableSongs();
            OnSongContainersUpdated?.Invoke();
        }


        [HarmonyPatch(typeof(MusicLibraryMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_OnEnable(MusicLibraryMenu __instance)
        {
            if (!HasAvailableAPSongUpdate)
                return;

            HasAvailableAPSongUpdate = false;
            __instance.RefreshAndReselect();
        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "Update")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_Update(MusicLibraryMenu __instance)
        {
            if (!HasAvailableAPSongUpdate)
                return;

            HasAvailableAPSongUpdate = false;
            __instance.APRefreshAndReselect(true);
        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "CreateNormalViewList")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_CreateNormalViewList_Postfix(MusicLibraryMenu __instance, List<ViewType> __result)
        {
            OnCreateNormalView?.Invoke(__instance, __result);
        }

        private static float _nextTrapCheckTime = 0f;
        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        public static void GameManager_Update_Postfix(GameManager __instance)
        {
            if (Time.unscaledTime >= _nextTrapCheckTime)
            {
                OnGameManagerUpdateThrottled?.Invoke(__instance);
                _nextTrapCheckTime = Time.unscaledTime + 0.2f;
            }
            var Modifiers = Keyboard.current != null &&
                Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.shiftKey.isPressed &&
                Keyboard.current.altKey.isPressed;

            if (Modifiers && Keyboard.current.qKey.wasPressedThisFrame)
            {
                var songRunner = AccessTools.Field(typeof(GameManager), "_songRunner").GetValue(__instance) as SongRunner;
                songRunner.SetSongTime(__instance.SongLength + 3.0, 0.0);

                var endSongMethod = typeof(GameManager).GetMethod("EndSong", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                IgnoreScoreForNextSong = true;
                endSongMethod?.Invoke(__instance, new object[] { });
            }
            if (Modifiers && Keyboard.current.rKey.wasPressedThisFrame)
                YargEngineActions.ForceRestartSong(ArchipelagoPlugin.APcontainer);
            if (Modifiers && Keyboard.current.sKey.wasPressedThisFrame)
                YargEngineActions.ApplyStarPowerItem(ArchipelagoPlugin.APcontainer);
            if (Modifiers && Keyboard.current.mKey.wasPressedThisFrame)
                YargEngineActions.ApplyRockMetertrapItem(ArchipelagoPlugin.APcontainer);
            if (Modifiers && Keyboard.current.dKey.wasPressedThisFrame)
                YargEngineActions.ApplyDeathLink(ArchipelagoPlugin.APcontainer, null);
        }
        [HarmonyPatch(typeof(DevWatermark), "Start")]
        [HarmonyPrefix]
        public static bool DevWatermark_Start_Prefix(DevWatermark __instance)
        {
            var watermarkText = typeof(DevWatermark)
                .GetField("_watermarkText", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(__instance);

            var textProperty = watermarkText.GetType().GetProperty("text");
            string currentText = string.Empty;

#if NIGHTLY
            currentText = $"<b>YARG {MonoSingleton<GlobalVariables>.Instance.CurrentVersion}</b> Nightly Build ({SystemInfo.graphicsDeviceType}). ";
#endif
            __instance.gameObject.SetActive(true);
            textProperty.SetValue(
                watermarkText,
                $"{currentText}Yarg Archipelago plugin V{ArchipelagoPlugin.pluginVersion}."
            );

            return false;
        }

        [HarmonyPatch(typeof(ToastManager), "ShowToast")]
        [HarmonyPrefix]
        public static bool ToastManager_ShowToast(ToastManager __instance, object type, string body, Action onClick) =>
            !APToastManager.HandleAPToasts(Convert.ToInt32(type), body, onClick, __instance);

        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        public static void AddAPButton(MainMenu __instance)
        {
            if (FirstAwake && ArchipelagoPlugin.ShowConnectionDialogOnStartup.Value)
                ArchipelagoPlugin.ToggleArchipelagoDialog();
            FirstAwake = false;

            var t = YargEngineActions.FindNav(__instance.gameObject, "Profiles") ?? 
                __instance.GetComponentInChildren<NavigatableButton>(true);
            var p = t.transform.parent;
            if (p.Find("AP_MenuEntry")) return;

            var go = UnityEngine.Object.Instantiate(t.gameObject, p);
            go.name = "AP_MenuEntry";
            go.transform.SetSiblingIndex(t.transform.GetSiblingIndex() - 1);
            go.SetActive(true);

            var b = go.GetComponent<NavigatableButton>();
            YargEngineActions.NavigatableButton_onClick.SetValue(b, new UnityEngine.UI.Button.ButtonClickedEvent());
            ((UnityEngine.UI.Button.ButtonClickedEvent)YargEngineActions.NavigatableButton_onClick.GetValue(b))
                .AddListener(ArchipelagoPlugin.ToggleArchipelagoDialog);

            var localizer = go.GetComponentInChildren<LocalizeText>(true);
            if (localizer != null) UnityEngine.Object.Destroy(localizer);
            YargEngineActions.TrySetText(go, "Archipelago");

            var g = t.NavigationGroup ?? t.GetComponentInParent<NavigationGroup>();
            g.AddNavigatable(b);
            ((List<NavigatableBehaviour>)YargEngineActions.NavigationGroup_navigatables.GetValue(g))
                .Sort((x, y) => x.transform.GetSiblingIndex() - y.transform.GetSiblingIndex());
        }

        [HarmonyPatch(typeof(MusicLibraryMenu), "OnEnable")]
        [HarmonyPostfix]
        public static void MusicLibraryMenu_OnEnable_Postfix(MusicLibraryMenu __instance)
        {
            // kill song-based reselection
            Traverse.Create(typeof(MusicLibraryMenu)).Field("CurrentlyPlaying").SetValue(null);
            Traverse.Create(__instance).Field("_currentSong").SetValue(null);
            Traverse.Create(typeof(MusicLibraryMenu)).Field("_forceGoToCurrentlyPlaying").SetValue(false);
            Traverse.Create(typeof(MusicLibraryMenu)).Field("_forceGoToSong").SetValue(null);

            var viewList = __instance.ViewList;
            if (viewList == null || viewList.Count == 0)
                return;

            int lastIndex = Traverse.Create(typeof(MusicLibraryMenu)).Field("_savedIndex").GetValue<int>();
            __instance.SelectedIndex = Mathf.Clamp(lastIndex, 0, viewList.Count - 1);
        }

        [HarmonyPatch( typeof(NavigationScheme.Entry), nameof(NavigationScheme.Entry.Invoke), [typeof(NavigationContext)])]
        [HarmonyPrefix]
        public static bool NavigationSchemeEntry_Invoke_Prefix(ref NavigationScheme.Entry __instance)
        {
            if (!__instance.Equals(NavigationScheme.Entry.NavigateSelect))
                return true;
            return NavigationGroup.CurrentNavigationGroup != null;
        }
    }
}
