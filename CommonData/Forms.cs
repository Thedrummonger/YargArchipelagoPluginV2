using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Menu.Persistent;

namespace YargArchipelagoPlugin
{
    public class ArchipelagoConnectionDialog : MonoBehaviour
    {
        public static ArchipelagoConnectionDialog Instance { get; private set; }

        [Header("State")]
        public bool Show = false;

        [Header("Defaults")]

        private bool showDeathlinkDropdown = false;
        APConnectionContainer connectionContainer;

        ConnectionDetails connectionDetails;


        private Rect _windowRect = new Rect(20, 20, 400, 220);

        public void Initialize(APConnectionContainer container)
        {
            connectionContainer = container;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            connectionDetails = new ConnectionDetails();
        }

        private void OnGUI()
        {
            if (!Show) return;

            _windowRect = GUI.Window(0xA1C4, _windowRect, DrawWindow, "Archipelago Connection");
        }
        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Address");
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.Address = GUILayout.TextField(connectionDetails.Address);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot Name");
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.SlotName = GUILayout.TextField(connectionDetails.SlotName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Password");
            using (new GUIEnabledScope(!connectionContainer.IsSessionConnected))
                connectionDetails.Password = GUILayout.PasswordField(connectionDetails.Password, '*');
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            string buttonText = connectionContainer.IsSessionConnected ? "Disconnect" : "Connect";
            if (GUILayout.Button(buttonText, GUILayout.Height(28)))
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                if (connectionContainer.IsSessionConnected)
                {
                    connectionContainer.Disconnect();
                    ToastManager.ToastInformation($"Disconnected from AP");
                }
                else
                {
                    ToastManager.ToastInformation($"Connecting to {connectionDetails.SlotName}@{connectionDetails.Address}");
                    connectionContainer.Connect(connectionDetails);
                }
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Close", GUILayout.Height(28)))
                Show = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool _prev;
            public GUIEnabledScope(bool enabled)
            {
                _prev = GUI.enabled;
                GUI.enabled = enabled;
            }
            public void Dispose() => GUI.enabled = _prev;
        }
    }
}
