using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using YARG.Menu.Persistent;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public class ArchipelagoEventManager
    {

    }

    public class APConnectionContainer
    {
        private ConnectionDetails LastUsedConnectionInfo = null;
        public ArchipelagoSession GetSession() => Session;
        private readonly ArchipelagoSession Session;
        public BepInEx.Logging.ManualLogSource logger;
        private Random SeededRNG = null;
        public HashSet<BaseYargAPItem> ReceivedSongs { get; } = new HashSet<BaseYargAPItem>();
        public HashSet<long> CheckedLocations { get; } = new HashSet<long>();
        public HashSet<StaticYargAPItem> ApItemsRecieved { get; } = new HashSet<StaticYargAPItem>();
        public DeathLinkService DeathLinkService { get; private set; } = null;

        private SongData CurrentlyPlaying = null;

        public YargSlotData SlotData { get; private set; }
        public APConnectionContainer(BepInEx.Logging.ManualLogSource logSource)
        {
            logger = logSource;
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
            var Session = ArchipelagoSessionFactory.CreateSession(Ip, Port);
            var Result = Session.TryConnectAndLogin("YAYARG", connectionDetails.SlotName, 
                Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems, password: connectionDetails.Password);
            if (Result is LoginFailure failure)
            {
                ToastManager.ToastError($"Failed to connect!\n{connectionDetails.SlotName}@{connectionDetails.Address}:\n{string.Join("\n", failure.Errors)}");
                return false;
            }
            LastUsedConnectionInfo = connectionDetails;
            ToastManager.ToastError($"Connected Archipelago!\n{connectionDetails.SlotName}@{connectionDetails.Address}");
            DeathLinkService = Session.CreateDeathLinkService();
            SeededRNG = new Random(GetAPSeed());
            SlotData = YargSlotData.Parse(Session.DataStorage.GetSlotData());
            Session.Items.ItemReceived += Items_ItemReceived;
            //Setup here
            return true;
        }

        private void Items_ItemReceived(Archipelago.MultiClient.Net.Helpers.ReceivedItemsHelper helper)
        {
            UpdateReceivedItems();
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
                        if (!ServerLocProxy.ContainsKey(item))
                            ServerLocProxy[item] = 0;
                        ServerLocProxy[item]++;
                    }
                    ApItemsRecieved.Add(new StaticYargAPItem(item, i.ItemId, i.Player.Slot, i.Player.Slot == 0 ? ServerLocProxy[item] : i.LocationId, i.LocationGame));
                    if (item == StaticItems.Victory)
                        Session.SetGoalAchieved();
                    continue;
                }
                if (SlotData.ItemIDtoAPData.TryGetValue(i.ItemId, out var songItem))
                {
                    ReceivedSongs.Add(new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame));
                    continue;
                }
                throw new Exception($"Error, received unknown item {i.ItemName} [{i.ItemId}]");
            }
        }

    }

    public class PersistantData
    {
        public HashSet<StaticYargAPItem> ApItemsUsed { get; } = new HashSet<StaticYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsPurchased { get; } = new HashSet<StaticYargAPItem>();

        public bool BroadcastSongName = false;

        public bool InGameAPChat = true;
        public ItemLog InGameItemLog = ItemLog.ToMe;

        public bool ManualMode = false;

        public bool CheatMode = false;

        /// <summary>
        /// This value tracks if deathlink was enabled in the YAML. it will never change
        /// </summary>
        public DeathLinkType YamlDeathLink = DeathLinkType.None;

        /// <summary>
        /// This value tracks the current death link mode. It can be changed in game independently of the yaml.
        /// </summary>
        public DeathLinkType DeathLinkMode = DeathLinkType.None;

        /// <summary>
        /// This value tracks if energylink was enabled in the YAML. it will never change
        /// </summary>
        public EnergyLinkType YamlEnergyLink = EnergyLinkType.None;

        /// <summary>
        /// This value tracks the current energylink mode. It can be changed in game independently of the yaml.
        /// </summary>
        public EnergyLinkType EnergyLinkMode = EnergyLinkType.None;
    }

    public class ConnectionDetails
    {
        public ConnectionDetails(string address, string slotname, string password)
        {
            Address = address;
            SlotName = slotname;
            Password = password;
        }
        public string Address { get; private set; }
        public string SlotName { get; private set; }
        public string Password { get; private set; }
    }
}
