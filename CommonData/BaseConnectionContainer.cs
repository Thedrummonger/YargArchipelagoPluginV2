using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public class BaseConnectionContainer 
    {
        public Action<string> LogInfo;
        public Action<string> LogWarning;
        public Action<string> LogError;
        public PersistantData seedConfig { get; protected set; }
        public bool IsPurchasingItem { get; internal set; }
        internal readonly System.Threading.SemaphoreSlim EnergyLinkRequests = new System.Threading.SemaphoreSlim(1, 1);
        public ArchipelagoSession Session;
        public ArchipelagoSession GetSession() => Session;
        public APSlotData SlotData;
        public bool ClientConnected => Session?.Socket != null && Session.Socket.Connected;
        public Dictionary<long, BaseYargAPItem> ReceivedSongUnlockItems { get; } = new Dictionary<long, BaseYargAPItem>();
        public Dictionary<SupportedInstrument, BaseYargAPItem> ReceivedInstruments { get; } = new Dictionary<SupportedInstrument, BaseYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsRecieved { get; } = new HashSet<StaticYargAPItem>();

        public Dictionary<StaticItems, ItemFlags> ItemPriorities = new Dictionary<StaticItems, ItemFlags>();
        public bool HasActiveSession => GetSession() != null;
        public virtual bool IsSessionConnected => HasActiveSession && Session.Socket.Connected;

        public bool GoalItemInPool(out bool Recieved, out BaseYargAPItem recieveInfo)
        {
            recieveInfo = null;
            Recieved = false;
            if (!IsSessionConnected) return false;
            Recieved = ReceivedSongUnlockItems.TryGetValue(SlotData.GoalData.UnlockItemID, out recieveInfo);
            // If we have not recieved the item, it was not in our starting items so it is in the pool
            // If we have recieved it from someone other than the server, it was in the pool.
            bool inPool = !Recieved || recieveInfo.SendingPlayerSlot > 0;
            return inPool;
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
                    if (!ItemPriorities.ContainsKey(item))
                        ItemPriorities[item] = i.Flags;
                }
                else if (InstrumentItemsById.TryGetValue(i.ItemId, out var instrument))
                    ReceivedInstruments[instrument] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else if (SlotData.SongUnlockIds.Contains(i.ItemId))
                    ReceivedSongUnlockItems[i.ItemId] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else
                    LogWarning?.Invoke($"Received unknown item {i.ItemName} [{i.ItemId}]");
            }
        }

        public HashSet<StaticYargAPItem> GetAllAquiredActionItems()
        {
            HashSet<StaticYargAPItem> Recieved = ApItemsRecieved;
            HashSet<StaticYargAPItem> Purchased = seedConfig is null ? new HashSet<StaticYargAPItem>() : seedConfig.ApItemsPurchased;
            return new HashSet<StaticYargAPItem>(Recieved.Union(Purchased));
        }
    }

    public class PersistantData
    {
        private BaseConnectionContainer parent;
        public HashSet<StaticYargAPItem> ApItemsUsed { get; } = new HashSet<StaticYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsPurchased { get; } = new HashSet<StaticYargAPItem>();

        public Dictionary<string, string> SongProxies { get; } = new Dictionary<string, string>();
        public Dictionary<string, CompletionRequirements> AdjustedDifficulties { get; } = new Dictionary<string, CompletionRequirements>();

        public bool ShowMissingInstruments = false;

        public bool ShowAPMenu = false;

        public bool ShowGoalStatus = false;

        public bool ShowPoolInfo = false;

        public bool InGameAPChat = true;

        public ItemLog InGameItemLog = ItemLog.ToMe;

        /// <summary>
        /// This value tracks the current death link mode. It can be changed in game independently of the yaml.
        /// </summary>
        public DeathLinkType DeathLinkMode = DeathLinkType.disabled;

        /// <summary>
        /// This value tracks when deathlink should trigger. It can be changed in game independently of the yaml.
        /// </summary>
        public DeathLinkTriggerType DeathLinkTrigger = DeathLinkTriggerType.both;

        /// <summary>
        /// This value tracks the current energylink mode. It can be changed in game independently of the yaml.
        /// </summary>
        public EnergyLinkType EnergyLinkMode = EnergyLinkType.disabled;

        public static PersistantData Load(BaseConnectionContainer container, ItemLog defaultItemLog, bool defaultShowChat)
        {
            if (!container.IsSessionConnected)
                return null;
            Directory.CreateDirectory(SeedConfigPath);
            var ConfigFile = Directory.GetFiles(SeedConfigPath)
                .FirstOrDefault(file => Path.GetFileName(file) == getSaveFileName(container));
            if (ConfigFile is null)
                return CreateNew();
            try 
            { 
                var configData = JsonConvert.DeserializeObject<PersistantData>(File.ReadAllText(ConfigFile));
                configData.parent = container;
                container.LogInfo?.Invoke($"Loaded Persistance Data\n{JsonConvert.SerializeObject(configData, Formatting.Indented)}");
                return configData;
            }
            catch 
            {
                return CreateNew();
            }

            PersistantData CreateNew() => new PersistantData
            {
                DeathLinkMode = container.SlotData.DeathLink,
                EnergyLinkMode = container.SlotData.EnergyLink,
                parent = container,
                InGameItemLog = defaultItemLog,
                InGameAPChat = defaultShowChat
            };
        }
        public void Save()
        {
            if (!parent.IsSessionConnected) return;
            Directory.CreateDirectory(SeedConfigPath);
            var path = Path.Combine(SeedConfigPath, getSaveFileName(parent));
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        private static string getSaveFileName(BaseConnectionContainer container) =>
            $"{container.GetSession()?.RoomState?.Seed}_{container.GetSession()?.Players?.ActivePlayer?.Slot}_" +
            $"{container.GetSession()?.Players?.ActivePlayer?.Slot}_{container.GetSession()?.Players?.ActivePlayer?.GetHashCode()}";

        public bool SendDlOnSongFail()
        {
            if (DeathLinkMode <= DeathLinkType.disabled) return false;
            if (DeathLinkTrigger == DeathLinkTriggerType.failed_requirements_only) return false;
            return true;
        }
        public bool SendDlOnRequirements()
        {
            if (DeathLinkMode <= DeathLinkType.disabled) return false;
            if (DeathLinkTrigger == DeathLinkTriggerType.song_fail_only) return false;
            return true;
        }

    }
}
