//Don't Let visual studios lie to me these are needed
using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.Profiling;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay;
using YARG.Menu.Persistent;
using YARG.Scores;
using YARG.Song;
using YargArchipelagoPlugin;
//----------------------------------------------------

namespace YargArchipelagoCommon
{
    public static class CommonData
    {

        private static T ParseEnum<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value);
        public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YARChipelago");
        public static string ConnectionCachePath => Path.Combine(DataFolder, "connection.json");
        public static string SongExportFile => Path.Combine(DataFolder, "SongExport.json");
        public static string userConfigFile => Path.Combine(DataFolder, "UserConfig.json");
        public static string SeedConfigPath => Path.Combine(DataFolder, "seeds");

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

        public static bool IsActionable(this StaticItems item)
        {
            var fieldInfo = item.GetType().GetField(item.ToString());
            return fieldInfo?.GetCustomAttribute<ActionableAttribute>() != null;
        }

        public enum SupportedInstrument
        {
            [Description("Five Fret Guitar")]
            FiveFretGuitar,
            [Description("Five Fret Bass")]
            FiveFretBass,
            [Description("Keys")]
            Keys,
            [Description("Six Fret Guitar")]
            SixFretGuitar,
            [Description("Six Fret Bass")]
            SixFretBass,
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

        public enum CompletionReq
        {
            Clear,
            OneStar,
            TwoStar,
            ThreeStar,
            FourStar,
            FiveStar,
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

            public PlayerInfo GetPlayerInfo(APConnectionContainer container) => 
                container.IsSessionConnected ? container.GetSession().Players.GetPlayerInfo(SendingPlayerSlot) : null;
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


        public class ConnectionDetails
        {
            public string Address { get; set; } = "localhost";//"Archipleago.gg:38281";
            public string SlotName { get; set; } = "TDMYarg";//string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class SongExportData
        {
            public string Name;
            public string Artist;
            public string SongChecksum;
            public string Source;
            public string Album;
            public string Genre;
            public string Charter;
            public Dictionary<SupportedInstrument, int> Difficulties = new Dictionary<SupportedInstrument, int>();
            [Newtonsoft.Json.JsonIgnore]
            public SongEntry YargSongEntry;
            public bool TryGetDifficulty(SupportedInstrument instrument, out int Difficulty) => Difficulties.TryGetValue(instrument, out Difficulty);
            public bool ValidForPool(SongPool pool) => TryGetDifficulty(pool.instrument, out var diff) && diff <= pool.max_difficulty && diff >= pool.min_difficulty;
            public static SongExportData FromSongEntry(SongEntry song)
            {
                return new SongExportData()
                {
                    Artist = RichTextUtils.StripRichTextTags(song.Artist),
                    Name = RichTextUtils.StripRichTextTags(song.Name),
                    SongChecksum = Convert.ToBase64String(song.Hash.HashBytes),
                    Difficulties = new Dictionary<CommonData.SupportedInstrument, int>(),
                    Source = song.Source.Original,
                    Album = song.Album,
                    Genre = song.Genre,
                    Charter = song.Charter,
                };
            }
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
            public CompletionRequirements completion_requirements { get; set; }

            public static SongPool FromJson(JObject json)
            {
                return new SongPool
                {
                    instrument = ParseEnum<SupportedInstrument>(json["instrument"].ToObject<string>()),
                    amount_in_pool = json["amount_in_pool"].ToObject<int>(),
                    min_difficulty = json["min_difficulty"].ToObject<int>(),
                    max_difficulty = json["max_difficulty"].ToObject<int>(),
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

            public SongPool GetPool(YargSlotData slotData) => slotData.Pools[PoolName];
            public string GetActiveHash(APConnectionContainer container) => HasProxy(container, out var ProxyHash) ? ProxyHash : Hash;
            public bool HasProxy(APConnectionContainer container, out string proxyHash) => container.seedConfig.SongProxies.TryGetValue(UniqueKey, out proxyHash);
            public SongEntry GetYargSongEntry(APConnectionContainer container)
            {
                var songObj = SongContainer.Songs.FirstOrDefault(x => Convert.ToBase64String(x.Hash.HashBytes) == GetActiveHash(container));
                if (songObj == null)
                    ToastManager.ToastError($"Song Hash {GetActiveHash(container)} was not a valid song in yarg!");
                return songObj;
            }
            public bool HadYargSongEntry(APConnectionContainer container, out SongEntry entry)
            {
                entry = GetYargSongEntry(container);
                return entry is SongEntry;
            }
            public bool WasActiveSongInGame(APConnectionContainer container, GameManager gameManager)
            {
                var SongHash = Convert.ToBase64String(gameManager.Song.Hash.HashBytes);
                return SongHash == GetActiveHash(container);
            }
            public CompletionRequirements GetCurrentCompletionRequirements(APConnectionContainer container)
            {
                if (container.seedConfig.AdjustedDifficulties.TryGetValue(UniqueKey, out var reqs))
                    return reqs;
                return GetPool(container.SlotData).completion_requirements;
            }
            public string GetDisplayName(APConnectionContainer container, bool IncludePool)
            {
                SongEntry YArgEntry = GetYargSongEntry(container);
                var SongName = YArgEntry is null ? $"{GetActiveHash(container)}" : $"{YArgEntry.Name} by {YArgEntry.Artist}";
                if (IncludePool)
                    SongName = $"[{PoolName}] {SongName}";
                return SongName;
            }

            public abstract bool IsSongUnlocked(APConnectionContainer connectionContainer);
            public abstract bool HasAvailableLocations(APConnectionContainer connectionContainer);
        }

        public class SongAPData : BaseAPSong
        {
            public long ExtraLocationID { get; set; }
            public long CompletionLocationID { get; set; }
            public override bool IsSongUnlocked(APConnectionContainer connectionContainer)
            {
                var pool = GetPool(connectionContainer.SlotData);
                return connectionContainer.ReceivedInstruments.ContainsKey(pool.instrument) &&
                    connectionContainer.ReceivedSongUnlockItems.ContainsKey(UnlockItemID);
            }
            public override bool HasAvailableLocations(APConnectionContainer connectionContainer)
            {
                if (!connectionContainer.GetSession().Locations.AllLocationsChecked.Contains(MainLocationID))
                    return true;
                if (ExtraLocationID > 0 && !connectionContainer.GetSession().Locations.AllLocationsChecked.Contains(ExtraLocationID))
                    return true;
                if (CompletionLocationID > 0 && !connectionContainer.GetSession().Locations.AllLocationsChecked.Contains(CompletionLocationID))
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

            public override bool HasAvailableLocations(APConnectionContainer connectionContainer) => 
                !connectionContainer.GetSession().Locations.AllLocationsChecked.Contains(MainLocationID);

            public override bool IsSongUnlocked(APConnectionContainer connectionContainer)
            {
                var HasUnlockItem = connectionContainer.ReceivedSongUnlockItems.ContainsKey(UnlockItemID);
                var CurrentSongCompletions = connectionContainer.ApItemsRecieved.Count(x => x.Type == StaticItems.SongCompletion);
                var HasEnoughCompletions = CurrentSongCompletions >= connectionContainer.SlotData.SetlistNeededForGoal;
                var CurrentFamePoints = connectionContainer.ApItemsRecieved.Count(x => x.Type == StaticItems.FamePoint);
                var HasEnoughFame = CurrentFamePoints >= connectionContainer.SlotData.FamePointsForGoal;
                return HasUnlockItem && HasEnoughFame && HasEnoughCompletions;
            }
        }

        public class YargSlotData
        {
            public int FamePointsForGoal { get; set; }
            public int SetlistNeededForGoal { get; set; }
            public GoalData GoalData { get; set; }
            public DeathLinkType DeathLink { get; set; }
            public EnergyLinkType EnergyLink { get; set; }
            public Dictionary<string, SongPool> Pools { get; set; } = new Dictionary<string, SongPool>();

            public HashSet<SongAPData> Songs { get; set; } = new HashSet<SongAPData>();
            public Dictionary<SupportedInstrument, SongAPData[]> SongsByInstrument { get; set; } = new Dictionary<SupportedInstrument, SongAPData[]>();
            public HashSet<long> SongUnlockIds { get; set; } = new HashSet<long>();

            public static YargSlotData Parse(Dictionary<string, object> slotData)
            {
                var result = new YargSlotData
                {
                    FamePointsForGoal = Convert.ToInt32(slotData["fame_points_for_goal"]),
                    SetlistNeededForGoal = Convert.ToInt32(slotData["setlist_needed_for_goal"]),
                    GoalData = GoalData.FromTuple(slotData["goal_data"] as JArray),
                    DeathLink = (DeathLinkType)Convert.ToInt32(slotData["death_link"]),
                    EnergyLink = (EnergyLinkType)Convert.ToInt32(slotData["energy_link"])
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
    }
}
