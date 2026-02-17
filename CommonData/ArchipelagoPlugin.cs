using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

namespace YargArchipelagoPlugin
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ArchipelagoPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "thedrummonger.yarg.archipelago";
        public const string pluginVersion = "0.2.4.0";
#if NIGHTLY
        public const string pluginName = "YARG Nightly Archipelago Plugin";
#else
        public const string pluginName = "YARG Archipelago Plugin";
#endif
        public static APConnectionContainer APcontainer;

        private ConfigEntry<Key> toggleKey;
        private ConfigEntry<bool> requireCtrl;
        private ConfigEntry<bool> requireShift;
        private ConfigEntry<bool> requireAlt;
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

        }

        private void Update()
        {
            if (!Application.isFocused)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (requireCtrl.Value && !(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) return;
            if (requireShift.Value && !(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return;
            if (requireAlt.Value && !(kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)) return;

            if (kb[toggleKey.Value].wasPressedThisFrame)
                ToggleArchipelagoDialog();
        }
        public static void ToggleArchipelagoDialog()
        {
            //YargAPUtils.TestFlags();
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
