using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace YargArchipelagoCommon
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ArchipelagoPlugin : BasePlugin
    {
        public const string pluginGuid = "thedrummonger.clonehero.archipelago";
        public const string pluginName = "Clone Hero Archipelago Plugin";
        public const string pluginVersion = Versions.CloneHero;
        internal static ManualLogSource PluginLog;
        public static APConnectionContainer APcontainer;
        public static ConfigEntry<KeyCode> ToggleKey;
        public static ConfigEntry<bool> RequireCtrl;
        public static ConfigEntry<bool> RequireShift;
        public static ConfigEntry<bool> RequireAlt;
        public static ConfigEntry<ItemLog> DefaultItemLog;
        public static ConfigEntry<bool> DefaultShowChat;
        public static ConfigEntry<bool> ShowConnectionDialogOnStartup;
        private static int _lastTickFrame = -1;
        public static void ApplyPatches() => new Harmony(pluginGuid).PatchAll(typeof(APPatches).Assembly);

        public override void Load()
        {
            PluginLog = Log;
            CloneHeroMappings.Initialize();
            PluginLog.LogInfo("Starting AP");
            APcontainer = new APConnectionContainer(PluginLog, AddListeners, RemoveListeners);
            ToggleKey = Config.Bind("Hotkeys", "ToggleDialogKey", KeyCode.F10, "Keyboard key used to toggle the connection dialog.");
            RequireCtrl = Config.Bind("Hotkeys", "RequireCtrl", false, "Require Ctrl to be held.");
            RequireShift = Config.Bind("Hotkeys", "RequireShift", false, "Require Shift to be held.");
            RequireAlt = Config.Bind("Hotkeys", "RequireAlt", false, "Require Alt to be held.");
            DefaultShowChat = Config.Bind("Chat", "DefaultShowChat", false, "The default Show Chat setting for new connections.");
            DefaultItemLog = Config.Bind("Chat", "DefaultItemLog", ItemLog.ToMe, "The default Item Log setting for new connections.");
            ShowConnectionDialogOnStartup = Config.Bind("Misc", "ShowConnectionDialogOnStartup", false, "Should the connection dialog be opened automatically on launch.");
            ApplyPatches();
        }

        private static void AddListeners(ArchipelagoEventManager eventManager)
        {
            APPatches.OnCreateNormalView += APPatches.InsertAPSongs;
            APPatches.OnRecordScore += EngineActions.FailedSong;
            APPatches.OnSongContainersUpdated += APcontainer.BuildSongLookup;
            APPatches.OnRecordScore += eventManager.TryCheckSongLocations;
            APPatches.OnRecordScore += eventManager.TryCheckSongGoalSong;
            APPatches.OnSongStarted += eventManager.SetSong;
            APPatches.OnSongEnded += eventManager.SetSong;
            APPatches.OnGameManagerUpdateThrottled += eventManager.ApplyPendingTrapsFiller;
        }

        private static void RemoveListeners(ArchipelagoEventManager eventManager)
        {
            APPatches.OnCreateNormalView -= APPatches.InsertAPSongs;
            APPatches.OnRecordScore -= EngineActions.FailedSong;
            APPatches.OnSongContainersUpdated -= APcontainer.BuildSongLookup;
            APPatches.OnRecordScore -= eventManager.TryCheckSongLocations;
            APPatches.OnRecordScore -= eventManager.TryCheckSongGoalSong;
            APPatches.OnSongStarted -= eventManager.SetSong;
            APPatches.OnSongEnded -= eventManager.SetSong;
            APPatches.OnGameManagerUpdateThrottled -= eventManager.ApplyPendingTrapsFiller;
        }


        internal static void Tick()
        {
            if (_lastTickFrame == Time.frameCount)
                return;
            _lastTickFrame = Time.frameCount;
            APcontainer?.TickMainThread();
            ArchipelagoConnectionDialog.UpdateUI();
        }
    }
}
