using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;
using YargArchipelagoCommon;

namespace YargArchipelagoPlugin
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ArchipelagoPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "thedrummonger.yarg.archipelago";
        public const string pluginVersion = "0.2.5.5";
#if NIGHTLY
        public const string pluginName = "YARG Nightly Archipelago Plugin";
#else
        public const string pluginName = "YARG Archipelago Plugin";
#endif
        public static APConnectionContainer APcontainer;

        public static ConfigEntry<Key> toggleKey;
        public static ConfigEntry<bool> requireCtrl;
        public static ConfigEntry<bool> requireShift;
        public static ConfigEntry<bool> requireAlt;

        public static ConfigEntry<CommonData.ItemLog> DefaultItemLog;
        public static ConfigEntry<bool> DefaultShowChat;

        public static ConfigEntry<bool> ShowConnectionDialogOnStartup;
        public void Awake()
        {
            var patcher = new Harmony(pluginGuid);
            patcher.PatchAll();

            Logger.LogInfo("Starting AP");
            APcontainer = new APConnectionContainer(Logger);

            toggleKey = Config.Bind("Hotkeys", "ToggleDialogKey", Key.F10, "Keyboard key used to toggle the connection dialog.");

            requireCtrl = Config.Bind("Hotkeys", "ToggleDialogRequireCtrl", false, "Require Ctrl to be held.");
            requireShift = Config.Bind("Hotkeys", "ToggleDialogRequireShift", false, "Require Shift to be held.");
            requireAlt = Config.Bind("Hotkeys", "ToggleDialogRequireAlt", false, "Require Alt to be held.");

            DefaultShowChat = Config.Bind("Chat", "DefaultShowChat", false, "The default Show Chat setting for new connections.");
            DefaultItemLog = Config.Bind("Chat", "DefaultItemLog", CommonData.ItemLog.ToMe, "The default Item Log setting for new connections.");

            ShowConnectionDialogOnStartup = Config.Bind("Misc", "ShowConnectionDialogOnStartup", false, "Should the connection dialog be opened automatically on launch.");

        }

        private void Update()
        {
            if (!Application.isFocused)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            bool DialogModifiersSatisfied =
                (!requireCtrl.Value || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) &&
                (!requireShift.Value || kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) &&
                (!requireAlt.Value || kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            if (DialogModifiersSatisfied && kb[toggleKey.Value].wasPressedThisFrame)
                ToggleArchipelagoDialog();

            var DevModifiersSatisfied = Keyboard.current != null &&
                Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.shiftKey.isPressed &&
                Keyboard.current.altKey.isPressed;

            if (DevModifiersSatisfied && Keyboard.current.tKey.wasPressedThisFrame)
                APToastManager.TestToasts();
        }
        public static void ToggleArchipelagoDialog()
        {
            var dialog = GetOrCreateApDialog();
            dialog.Show = !dialog.Show;
        }
        private static ArchipelagoConnectionDialog GetOrCreateApDialog()
        {
            if (ArchipelagoConnectionDialog.Instance != null)
                return ArchipelagoConnectionDialog.Instance;

            var DialogObject = new GameObject("ArchipelagoConnectionDialog");
            DontDestroyOnLoad(DialogObject);
            var dialog = DialogObject.AddComponent<ArchipelagoConnectionDialog>();
            dialog.Initialize(APcontainer);
            return dialog;
        }
    }
}
