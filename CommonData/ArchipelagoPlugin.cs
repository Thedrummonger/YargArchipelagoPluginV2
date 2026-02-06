using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Windows;

namespace YargArchipelagoPlugin
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ArchipelagoPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "thedrummonger.yarg.archipelago";
        public const string pluginVersion = "0.2.2.0";
#if NIGHTLY
        public const string pluginName = "YARG Nightly Archipelago Plugin";
#else
        public const string pluginName = "YARG Archipelago Plugin";
#endif
        public static APConnectionContainer APcontainer;
        public void Awake()
        {
            var patcher = new Harmony(pluginGuid);
            patcher.PatchAll();

            Logger.LogInfo("Starting AP");
            APcontainer = new APConnectionContainer(Logger);

        }

        private void Update()
        {
            if (!Application.isFocused) 
                return;

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame)
                ToggleArchipelagoDialog();
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
