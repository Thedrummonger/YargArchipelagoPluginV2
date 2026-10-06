using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public static class APWorldData
    {
        public const string Game = "YAYARG";
        public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YARChipelago");
        public static string ConnectionCachePath => Path.Combine(DataFolder, "connection.json");
        public static string userConfigFile => Path.Combine(DataFolder, "UserConfig.json");
        public static string SeedConfigPath => Path.Combine(DataFolder, "seeds");

        internal static T ParseEnum<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value);

        //This must be set to the same value in the data_register file in the APWorld
        private static int _itemIDOffsetCounter = 100;

        public static readonly Dictionary<string, StaticItems> StaticItemsByName =
            Enum.GetValues(typeof(StaticItems))
                .Cast<StaticItems>()
                .ToDictionary(item => item.GetDescription(), item => item);

        public static readonly Dictionary<long, StaticItems> StaticItemsById =
            Enum.GetValues(typeof(StaticItems))
                .Cast<StaticItems>()
                .ToDictionary(item => (long)_itemIDOffsetCounter++, item => item);

        public static readonly Dictionary<StaticItems, long> StaticItemIDbyValue =
            StaticItemsById.ToDictionary(x => x.Value, x => x.Key);

        public static readonly Dictionary<string, SupportedInstrument> InstrumentItemsByName =
            Enum.GetValues(typeof(SupportedInstrument))
                .Cast<SupportedInstrument>()
                .ToDictionary(item => item.GetDescription(), item => item);

        public static readonly Dictionary<long, SupportedInstrument> InstrumentItemsById =
            Enum.GetValues(typeof(SupportedInstrument))
                .Cast<SupportedInstrument>()
                .ToDictionary(item => (long)_itemIDOffsetCounter++, item => item);

        public static readonly Dictionary<SupportedInstrument, long> InstrumentIDbyValue =
            InstrumentItemsById.ToDictionary(x => x.Value, x => x.Key);
    }

    public class CompletionRequirements
    {
        public CompletionReq reward1_req { get; set; }
        public SupportedDifficulty reward1_diff { get; set; }
        public CompletionReq reward2_req { get; set; }
        public SupportedDifficulty reward2_diff { get; set; }

        public static CompletionRequirements FromJson(JObject json)
        {
            return new CompletionRequirements
            {
                reward1_req = ParseEnum<CompletionReq>(json["reward1_req"].ToObject<string>()),
                reward1_diff = ParseEnum<SupportedDifficulty>(json["reward1_diff"].ToObject<string>()),
                reward2_req = ParseEnum<CompletionReq>(json["reward2_req"].ToObject<string>()),
                reward2_diff = ParseEnum<SupportedDifficulty>(json["reward2_diff"].ToObject<string>())
            };
        }
    }

    public class SongPool
    {
        public SupportedInstrument instrument { get; set; }
        public long amount_in_pool { get; set; }
        public long min_difficulty { get; set; }
        public long max_difficulty { get; set; }
        public long min_time { get; set; }
        public long max_time { get; set; }
        public CompletionRequirements completion_requirements { get; set; }

        public static SongPool FromJson(JObject json)
        {
            return new SongPool
            {
                instrument = ParseEnum<SupportedInstrument>(json["instrument"].ToObject<string>()),
                amount_in_pool = json.Value<int>("amount_in_pool"),
                min_difficulty = json.Value<int>("min_difficulty"),
                max_difficulty = json.Value<int>("max_difficulty"),
                min_time = json.Value<int?>("min_time") ?? 0,
                max_time = json.Value<int?>("max_time") ?? int.MaxValue,
                completion_requirements = CompletionRequirements.FromJson(json["completion_requirements"] as JObject)
            };
        }
    }

    public abstract class BaseAPSong
    {
        public string Hash { get; set; }
        public string PoolName { get; set; }
        public long MainLocationID { get; set; }
        public long UnlockItemID { get; set; }

        public string UniqueKey => $"{PoolName}[{Hash}]";

        public SongPool GetPool(APSlotData slotData) => slotData.Pools[PoolName];
        public string GetActiveHash(BaseConnectionContainer container) => HasProxy(container, out var ProxyHash) ? ProxyHash : Hash;
        public bool HasProxy(BaseConnectionContainer container, out string proxyHash) => 
            container.seedConfig.SongProxies.TryGetValue(UniqueKey, out proxyHash);

        public CompletionRequirements GetCurrentCompletionRequirements(BaseConnectionContainer container)
        {
            if (container.seedConfig.AdjustedDifficulties.TryGetValue(UniqueKey, out var reqs))
                return reqs;
            return GetPool(container.SlotData).completion_requirements;
        }
        public virtual bool IsSongUnlocked(BaseConnectionContainer connectionContainer)
        {
            var HasUnlockItem = connectionContainer.ReceivedSongUnlockItems.ContainsKey(UnlockItemID);
            var HasInstrument = connectionContainer.ReceivedInstruments.ContainsKey(GetPool(connectionContainer.SlotData).instrument);
            return HasUnlockItem && HasInstrument;
        }

        public abstract bool VisableInSongMenu(BaseConnectionContainer connectionContainer);

        public abstract bool HasAvailableLocations(BaseConnectionContainer connectionContainer);
        public abstract bool HasAvailableLocations(ArchipelagoSession session);
    }

    public class SongAPData : BaseAPSong
    {
        public long ExtraLocationID { get; set; }
        public long CompletionLocationID { get; set; }
        public override bool HasAvailableLocations(BaseConnectionContainer connectionContainer) => HasAvailableLocations(connectionContainer.GetSession());
        public override bool HasAvailableLocations(ArchipelagoSession session)
        {
            if (!session.Locations.AllLocationsChecked.Contains(MainLocationID))
                return true;
            if (ExtraLocationID > 0 && !session.Locations.AllLocationsChecked.Contains(ExtraLocationID))
                return true;
            if (CompletionLocationID > 0 && !session.Locations.AllLocationsChecked.Contains(CompletionLocationID))
                return true;
            return false;
        }
        public static SongAPData FromTuple(JArray tuple, string hash, string pool)
        {
            return new SongAPData
            {
                Hash = hash,
                PoolName = pool,
                MainLocationID = tuple[0].ToObject<long>(),
                ExtraLocationID = tuple[1].ToObject<long>(),
                CompletionLocationID = tuple[2].ToObject<long>(),
                UnlockItemID = tuple[3].ToObject<long>()
            };
        }

        public override bool VisableInSongMenu(BaseConnectionContainer connectionContainer) =>
            connectionContainer.ReceivedSongUnlockItems.ContainsKey(UnlockItemID) && HasAvailableLocations(connectionContainer);

        public bool IsHinted(BaseConnectionContainer connectionContainer, out Hint[] Hints)
        {
            Hints = [];
            if (!connectionContainer.IsSessionConnected) return false;
            var ServerHints = connectionContainer.GetSession().Hints.GetHints();
            Hints = [.. ServerHints.Where(x => x.LocationId == MainLocationID || x.LocationId == ExtraLocationID)];
            return Hints.Any();
        }
    }

    public class GoalData : BaseAPSong
    {
        public static GoalData FromTuple(JArray tuple)
        {
            return new GoalData
            {
                Hash = tuple[0].ToObject<string>(),
                PoolName = tuple[1].ToObject<string>(),
                MainLocationID = tuple[2].ToObject<long>(),
                UnlockItemID = tuple[3].ToObject<long>()
            };
        }

        public override bool HasAvailableLocations(BaseConnectionContainer connectionContainer) => HasAvailableLocations(connectionContainer.GetSession());
        public override bool HasAvailableLocations(ArchipelagoSession session) => 
            !session.Locations.AllLocationsChecked.Contains(MainLocationID);

        public override bool IsSongUnlocked(BaseConnectionContainer connectionContainer)
        {
            if (!base.IsSongUnlocked(connectionContainer))
                return false;

            var CurrentSongCompletions = connectionContainer.ApItemsRecieved.Count(x => x.Type == StaticItems.SongCompletion);
            var HasEnoughCompletions = CurrentSongCompletions >= connectionContainer.SlotData.SetlistNeededForGoal;
            var CurrentFamePoints = connectionContainer.ApItemsRecieved.Count(x => x.Type == StaticItems.FamePoint);
            var HasEnoughFame = CurrentFamePoints >= connectionContainer.SlotData.FamePointsForGoal;

            return HasEnoughFame && HasEnoughCompletions;
        }

        public override bool VisableInSongMenu(BaseConnectionContainer connectionContainer) => IsSongUnlocked(connectionContainer);
    }

    public class APSlotData
    {
        public int FamePointsForGoal { get; set; }
        public int SetlistNeededForGoal { get; set; }
        public GoalData GoalData { get; set; }
        public DeathLinkType DeathLink { get; set; }
        public EnergyLinkType EnergyLink { get; set; }
        public Dictionary<string, SongPool> Pools { get; set; } = new Dictionary<string, SongPool>();

        public Version APWorldVersion { get; set; }

        public HashSet<SongAPData> Songs { get; set; } = new HashSet<SongAPData>();
        public Dictionary<SupportedInstrument, SongAPData[]> SongsByInstrument { get; set; } = new Dictionary<SupportedInstrument, SongAPData[]>();
        public HashSet<long> SongUnlockIds { get; set; } = new HashSet<long>();

        public static APSlotData Parse(Dictionary<string, object> slotData)
        {
            var result = new APSlotData
            {
                FamePointsForGoal = Convert.ToInt32(slotData["fame_points_for_goal"]),
                SetlistNeededForGoal = Convert.ToInt32(slotData["setlist_needed_for_goal"]),
                GoalData = GoalData.FromTuple(slotData["goal_data"] as JArray),
                DeathLink = (DeathLinkType)Convert.ToInt32(slotData["death_link"]),
                EnergyLink = (EnergyLinkType)Convert.ToInt32(slotData["energy_link"]),
                APWorldVersion = slotData.TryGetValue("apworld_version", out var v) && Version.TryParse(v?.ToString(), out var parsed)
                    ? parsed : new Version(2, 3, 0)
            };

            var poolsJson = slotData["pools"] as JObject;
            foreach (var pool in poolsJson)
                result.Pools[pool.Key] = SongPool.FromJson(pool.Value as JObject);

            var songDataJson = slotData["song_data"] as JObject;
            foreach (var songHash in songDataJson)
            {
                var poolsDataJson = songHash.Value as JObject;
                foreach (var APData in poolsDataJson)
                    result.Songs.Add(SongAPData.FromTuple(APData.Value as JArray, songHash.Key, APData.Key));
            }

            result.SongsByInstrument = result.Songs.GroupBy(x => x.GetPool(result).instrument).ToDictionary(x => x.Key, x => x.ToArray());
            result.SongUnlockIds = result.Songs.Select(x => x.UnlockItemID).ToHashSet();
            result.SongUnlockIds.Add(result.GoalData.UnlockItemID);

            return result;
        }
    }

    public static class EnumDescriptions
    {
        public static string GetDescription(this Enum value) =>
            value.GetType().GetField(value.ToString())?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description ?? value.ToString();

        public static T Next<T>(this T currentValue) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int currentIndex = Array.IndexOf(values, currentValue);
            int nextIndex = (currentIndex + 1) % values.Length;
            return values[nextIndex];
        }

        public static bool IsActionable(this StaticItems item)
        {
            var fieldInfo = item.GetType().GetField(item.ToString());
            return fieldInfo?.GetCustomAttribute<ActionableAttribute>() != null;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ActionableAttribute : Attribute { }

    public enum StaticItems
    {
        [Description("Victory")]
        Victory,
        [Description("Fame Point")]
        FamePoint,
        [Description("Song Completion")]
        SongCompletion,
        [Description("Star Power"), Actionable]
        StarPower,
        [Description("Swap Song (Random)")]
        SwapRandom,
        [Description("Swap Song (Pick)")]
        SwapPick,
        [Description("Lower Difficulty")]
        LowerDifficulty,
        [Description("Restart Trap"), Actionable]
        TrapRestart,
        [Description("Rock Meter Trap"), Actionable]
        TrapRockMeter,
        [Description("Failure Prevention)")]
        FailPrevention,
        [Description("Nothing")]
        Nothing
    }

    public enum SupportedInstrument
    {
        [Description("Five Fret Guitar")]
        FiveFretGuitar,
        [Description("Five Fret Bass")]
        FiveFretBass,
        [Description("Keys")]
        Keys,
        [Description("Four Lane Drums")]
        FourLaneDrums,
        [Description("Pro Drums")]
        ProDrums,
        [Description("Five Lane Drums")]
        FiveLaneDrums,
        [Description("Pro Keys")]
        ProKeys,
        [Description("Vocals")]
        Vocals,
        [Description("Harmony")]
        Harmony
    }

    public enum CompletionReq
    {
        Clear,
        OneStar,
        TwoStar,
        ThreeStar,
        FourStar,
        FiveStar,
        SixStar,
        GoldStar,
        FullCombo
    }

    public enum SupportedDifficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4,
    }

    public enum ItemLog
    {
        [Description("No Items")]
        None = 0,
        [Description("My Items")]
        ToMe = 1,
        [Description("All Items")]
        All = 2,
    }

    public enum DeathLinkType
    {
        [Description("Disabled")]
        disabled = 0,
        [Description("Rock Meter")]
        rock_meter = 1,
        [Description("Instant Fail")]
        instant_fail = 2,
    }

    public enum DeathLinkTriggerType
    {
        [Description("Both")]
        both = 0,
        [Description("Song Fail Only")]
        song_fail_only = 1,
        [Description("Failed Reqs Only")]
        failed_requirements_only = 2,
    }

    public enum EnergyLinkType
    {
        [Description("Disabled")]
        disabled = 0,
        [Description("Check Song")]
        check_song = 1,
        [Description("Other Song")]
        other_song = 2,
        [Description("Any Song")]
        any_song = 3,
    }

    public class BaseYargAPItem
    {
        public BaseYargAPItem(long itemID, int sendingSlot, long sendingLocationID, string Game)
        {
            ItemID = itemID;
            SendingPlayerGame = Game;
            SendingPlayerLocation = sendingLocationID;
            SendingPlayerSlot = sendingSlot;
        }
        public long ItemID;
        public int SendingPlayerSlot;
        public long SendingPlayerLocation;
        public string SendingPlayerGame;

        private PlayerInfo GetPlayerCache = null;
        public PlayerInfo GetPlayerInfo(BaseConnectionContainer container)
        {
            if (!container.IsSessionConnected) return null;
            GetPlayerCache ??= container.GetSession().Players.GetPlayerInfo(SendingPlayerSlot);
            return GetPlayerCache;
        }
        public string GetLocationsName(BaseConnectionContainer container)
        {
            if (!container.IsSessionConnected) return null;
            return container.GetSession().Locations.GetLocationNameFromId(SendingPlayerLocation, SendingPlayerGame);
        }
        public string GetItemInfo(BaseConnectionContainer container)
        {
            if (!container.IsSessionConnected) return null;
            return container.GetSession().Items.GetItemName(ItemID, container.GetSession().ConnectionInfo.Game);
        }
    }

    public class StaticYargAPItem : BaseYargAPItem
    {
        public StaticItems Type;

        public StaticYargAPItem(StaticItems type, long itemID, int sendingSlot, long sendingLocationID, string Game) : base(itemID, sendingSlot, sendingLocationID, Game)
        {
            Type = type;
        }

        private string FillerHash() => $"{Type}|{ItemID}|{SendingPlayerSlot}|{SendingPlayerLocation}|{SendingPlayerGame}";

        public override int GetHashCode() => FillerHash().GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;
            StaticYargAPItem other = (StaticYargAPItem)obj;
            return FillerHash() == other.FillerHash();
        }

        public static bool operator ==(StaticYargAPItem left, StaticYargAPItem right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(StaticYargAPItem left, StaticYargAPItem right) => !(left == right);
    }

    public class ConnectionDetails
    {
        public string Address { get; set; } = "archipelago.gg:38281";
        public string SlotName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public static bool TryParseAddress(string address, out Uri result, out string error)
        {
            result = null;
            error = "Enter a server address.";
            if (string.IsNullOrWhiteSpace(address)) return false;
            var value = address.Trim();
            if (!value.Contains("://")) value = "ws://" + value;
            error = "Enter a valid server address and port.";
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss") || string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo)) return false;
            var authority = value.Substring(value.IndexOf("://", StringComparison.Ordinal) + 3).Split('/')[0];
            bool hasPort = authority.StartsWith("[") ? authority.Contains("]:") : authority.IndexOf(':') >= 0;
            var builder = new UriBuilder(uri);
            if (!hasPort) builder.Port = 38281;
            if (builder.Port < 1) return false;
            result = builder.Uri;
            error = null;
            return true;
        }
        public void Save()
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(ConnectionCachePath, Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented));
        }
        public static ConnectionDetails Load()
        {
            Directory.CreateDirectory(DataFolder);
            if (File.Exists(ConnectionCachePath))
            {
                try
                {
                    var Cache = Newtonsoft.Json.JsonConvert.DeserializeObject<ConnectionDetails>(File.ReadAllText(ConnectionCachePath));
                    if (Cache != null)
                        return Cache;
                }
                catch { }
            }
            return new ConnectionDetails();
        }
    }

    public sealed class CrossGameDictionary
    {
        public SortedDictionary<string, SortedDictionary<string, string>> CloneHeroToYarg { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, SortedDictionary<string, string>> YargToCloneHero { get; set; } = new(StringComparer.Ordinal);
    }
}
