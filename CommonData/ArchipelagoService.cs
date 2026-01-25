using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Menu.Persistent;
using YARG.Song;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public class APConnectionContainer
    {
        public ConnectionDetails LastUsedConnectionInfo { get; private set; } = null;
        public ArchipelagoSession GetSession() => Session;
        private ArchipelagoSession Session;
        public BepInEx.Logging.ManualLogSource logger;
        private Random SeededRNG = null;
        public Dictionary<long, BaseYargAPItem> ReceivedSongUnlockItems { get; } = new Dictionary<long, BaseYargAPItem>();
        public Dictionary<SupportedInstrument, BaseYargAPItem> ReceivedInstruments { get; } = new Dictionary<SupportedInstrument, BaseYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsRecieved { get; } = new HashSet<StaticYargAPItem>();
        public HashSet<long> CheckedLocations { get; } = new HashSet<long>();
        public DeathLinkService DeathLinkService { get; private set; } = null;

        private DateTime CurrentSongStartTime = DateTime.Now;
        private GameManager CurrentlyPlaying = null;
        public void SetCurrentSong(GameManager game)
        {
            CurrentSongStartTime = DateTime.Now;
            CurrentlyPlaying = game;
        }
        public void ClearCurrentSong() => CurrentlyPlaying = null;
        public bool IsInSong(out GameManager song, out TimeSpan buffer)
        {
            song = null;
            buffer = TimeSpan.Zero;
            if (CurrentlyPlaying is null)
                return false;
            song = CurrentlyPlaying;
            buffer = DateTime.Now - CurrentSongStartTime;
            return true;
        }

        private readonly ArchipelagoEventManager eventManager;

        public PersistantData seedConfig { get; private set; } = null;

        public bool HasActiveSession => Session != null;
        public bool IsSessionConnected => HasActiveSession && Session.Socket.Connected;

        public YargSlotData SlotData { get; private set; }

        public SyncTimer APSyncTimer { get; private set; }
        public APConnectionContainer(ManualLogSource logSource)
        {
            logger = logSource;
            eventManager = new ArchipelagoEventManager(this);
            APSyncTimer = new SyncTimer();
            APSyncTimer.StartTimer();
        }

        private int GetAPSeed()
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Session.RoomState.Seed));
                return BitConverter.ToInt32(hash, 0);
            }
        }

        public bool Connect(ConnectionDetails connectionDetails)
        {
            var (Ip, Port) = YargAPUtils.ParseIpAddress(connectionDetails.Address);
            if (Ip is null)
                return false;
            var tempSession = ArchipelagoSessionFactory.CreateSession(Ip, Port);

            var Result = tempSession.TryConnectAndLogin("YAYARG", connectionDetails.SlotName,
                Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems, new Version(0,6,1), password: connectionDetails.Password);
            if (Result is LoginFailure failure)
            {
                ToastManager.ToastError($"Failed to connect!\n{connectionDetails.SlotName}@{connectionDetails.Address}:\n{string.Join("\n", failure.Errors)}");
                return false;
            }
            Session = tempSession;
            LastUsedConnectionInfo = connectionDetails;
            ToastManager.ToastInformation($"Connected Archipelago!\n{connectionDetails.SlotName}@{connectionDetails.Address}");
            DeathLinkService = Session.CreateDeathLinkService();
            SeededRNG = new Random(GetAPSeed());
            SlotData = YargSlotData.Parse(Session.DataStorage.GetSlotData());
            File.WriteAllText(Path.Combine(CommonData.DataFolder, "Debug.json"), JsonConvert.SerializeObject(SlotData, Formatting.Indented));
            seedConfig = PersistantData.Load(this);
            if (seedConfig is null)
            {
                seedConfig = new PersistantData
                {
                    DeathLinkMode = (DeathLinkType)SlotData.DeathLink,
                    EnergyLinkMode = (EnergyLinkType)SlotData.EnergyLink
                };
            }
            AddListeners();
            eventManager.UpdateAPData();
            UpdateDeathLinkTags();
            return true;
        }

        public void Disconnect()
        {
            RemoveListeners();
            if (Session?.Socket?.Connected ?? false)
                Session.Socket.DisconnectAsync();
            ReceivedSongUnlockItems.Clear();
            ApItemsRecieved.Clear();
            ReceivedInstruments.Clear();
            CheckedLocations.Clear();
            DeathLinkService = null;
            SeededRNG = null;
            SlotData = null;
            seedConfig = null;
            Session = null;
        }

        private bool _Listening = false;
        public void AddListeners()
        {
            if (_Listening) return;
            APSyncTimer.ConstantCallback += eventManager.VerifyServerConnection;
            APSyncTimer.ConstantCallback += eventManager.ApplyPendingTrapsFiller;
            APSyncTimer.OnUpdateCallback += eventManager.UpdateAPData;

            Session.Items.ItemReceived += eventManager.Items_ItemReceived;
            Session.Locations.CheckedLocationsUpdated += eventManager.Locations_CheckedLocationsUpdated;
            Session.MessageLog.OnMessageReceived += eventManager.RelayChatToYARG;

            DeathLinkService.OnDeathLinkReceived += eventManager.OnDeathLinkReceived;

            APPatches.OnCreateNormalView += eventManager.InsertAPSongs;
            APPatches.OnSongStarted += eventManager.SetSong;
            APPatches.OnSongEnded += eventManager.SetSong;
            APPatches.OnRecordScore += eventManager.TryCheckSongLocations;
            APPatches.OnRecordScore += eventManager.TryCheckSongGoalSong;
            APPatches.OnSongFail += eventManager.FailedSong;
            _Listening = true;
        }

        public void RemoveListeners()
        {
            if (!_Listening) return;
            APSyncTimer.ConstantCallback -= eventManager.VerifyServerConnection;
            APSyncTimer.ConstantCallback -= eventManager.ApplyPendingTrapsFiller;
            APSyncTimer.OnUpdateCallback -= eventManager.UpdateAPData;

            Session.Items.ItemReceived -= eventManager.Items_ItemReceived;
            Session.Locations.CheckedLocationsUpdated -= eventManager.Locations_CheckedLocationsUpdated;
            Session.MessageLog.OnMessageReceived -= eventManager.RelayChatToYARG;

            DeathLinkService.OnDeathLinkReceived -= eventManager.OnDeathLinkReceived;

            APPatches.OnCreateNormalView -= eventManager.InsertAPSongs;
            APPatches.OnSongStarted -= eventManager.SetSong;
            APPatches.OnSongEnded -= eventManager.SetSong;
            APPatches.OnRecordScore -= eventManager.TryCheckSongLocations;
            APPatches.OnRecordScore -= eventManager.TryCheckSongGoalSong;
            APPatches.OnSongFail -= eventManager.FailedSong;
            _Listening = false;
        }

        public void UpdateCheckedLocations()
        {
            foreach (var i in Session.Locations.AllLocationsChecked)
                CheckedLocations.Add(i);
        }

        public void UpdateReceivedItems()
        {
            Dictionary<StaticItems, int> ServerLocProxy = new Dictionary<StaticItems, int>();
            foreach (var i in Session.Items.AllItemsReceived)
            {
                if (StaticItemsById.TryGetValue(i.ItemId, out var item))
                {
                    if (i.Player.Slot == 0)
                    {
                        if (!ServerLocProxy.ContainsKey(item)) ServerLocProxy[item] = 0;
                        ServerLocProxy[item]++;
                    }
                    ApItemsRecieved.Add(new StaticYargAPItem(item, i.ItemId, i.Player.Slot, i.Player.Slot == 0 ? ServerLocProxy[item] : i.LocationId, i.LocationGame));
                    if (item == StaticItems.Victory) Session.SetGoalAchieved();
                }
                else if (InstrumentItemsById.TryGetValue(i.ItemId, out var instrument))
                    ReceivedInstruments[instrument] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else if (SlotData.ItemIDtoAPData.ContainsKey(i.ItemId))
                    ReceivedSongUnlockItems[i.ItemId] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else if (i.ItemId == SlotData.GoalData.UnlockItemID)
                    ReceivedSongUnlockItems[i.ItemId] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else if (i.ItemName.ToLower().StartsWith("song pack"))
                    ReceivedSongUnlockItems[i.ItemId] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else
                    throw new Exception($"Error, received unknown item {i.ItemName} [{i.ItemId}]");
            }
        }

        public void UpdateDeathLinkTags()
        {
            if (!IsSessionConnected)
                return;
            if (seedConfig.DeathLinkMode > DeathLinkType.None)
                DeathLinkService.EnableDeathLink();
            else
                DeathLinkService.DisableDeathLink();
        }

    }

    public class PersistantData
    {
        public HashSet<StaticYargAPItem> ApItemsUsed { get; } = new HashSet<StaticYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsPurchased { get; } = new HashSet<StaticYargAPItem>();

        public bool InGameAPChat = true;

        public ItemLog InGameItemLog = ItemLog.ToMe;

        public bool CheatMode = false;

        /// <summary>
        /// This value tracks the current death link mode. It can be changed in game independently of the yaml.
        /// </summary>
        public DeathLinkType DeathLinkMode = DeathLinkType.None;

        /// <summary>
        /// This value tracks the current energylink mode. It can be changed in game independently of the yaml.
        /// </summary>
        public EnergyLinkType EnergyLinkMode = EnergyLinkType.None;

        public static PersistantData Load(APConnectionContainer container)
        {
            if (!container.IsSessionConnected)
                return null;
            Directory.CreateDirectory(SeedConfigPath);
            var ConfigFile = Directory.GetFiles(SeedConfigPath)
                .FirstOrDefault(file => Path.GetFileName(file) == getSaveFileName(container));
            if (ConfigFile is null)
                return null;
            try { 
                var configData = JsonConvert.DeserializeObject<PersistantData>(File.ReadAllText(ConfigFile));
                return configData;
            }
            catch { return null; }
        }
        public void Save(APConnectionContainer container)
        {
            if (!container.IsSessionConnected) return;
            Directory.CreateDirectory(SeedConfigPath);
            var path = Path.Combine(SeedConfigPath, getSaveFileName(container));
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        private static string getSaveFileName(APConnectionContainer container) =>
            $"{container.GetSession()?.RoomState?.Seed}_{container.GetSession()?.Players?.ActivePlayer?.Slot}_" +
            $"{container.GetSession()?.Players?.ActivePlayer?.Slot}_{container.GetSession()?.Players?.ActivePlayer?.GetHashCode()}";
    }

    public class ConnectionDetails
    {
        public string Address { get; set; } = "localhost";//"Archipleago.gg:38281";
        public string SlotName { get; set; } = "TDMYarg";//string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
