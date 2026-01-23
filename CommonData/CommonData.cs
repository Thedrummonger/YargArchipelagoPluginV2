//Don't Let visual studios lie to me these are needed
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
//----------------------------------------------------

namespace YargArchipelagoCommon
{
    public class CommonData
    {
        public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YARChipelago");
        public static string ConnectionCachePath => Path.Combine(DataFolder, "connection.json");
        public static string SongExportFile => Path.Combine(DataFolder, "SongExport.json");
        public static string userConfigFile => Path.Combine(DataFolder, "UserConfig.json");
        public static string SeedConfigPath => Path.Combine(DataFolder, "seeds");
        public enum SupportedInstrument
        {
            // Instruments are reserved in multiples of 10
            // 0-9: 5-fret guitar\
            FiveFretGuitar = 0,
            FiveFretBass = 1,
            Keys = 4,

            // 10-19: 6-fret guitar
            SixFretGuitar = 10,
            SixFretBass = 11,

            // 20-29: Drums
            FourLaneDrums = 20,
            ProDrums = 21,
            FiveLaneDrums = 22,

            // 30-39: Pro instruments
            ProKeys = 34,

            // 40-49: Vocals
            Vocals = 40,
            Harmony = 41,
        }

        public class SongData
        {
            public string Name;
            public string Artist;
            public string Album;
            public string Charter;
            public string Path;
            public string SongChecksum;
            public Dictionary<SupportedInstrument, int> Difficulties = new Dictionary<SupportedInstrument, int>();
            public bool TryGetDifficulty(SupportedInstrument instrument, out int Difficulty) => Difficulties.TryGetValue(instrument, out Difficulty);
        }

        public class SongCompletedData
        {
            public SongCompletedData(SongData Song, bool Passed, SongParticipantInfo[] participants, long bandScore) { 
                SongData = Song; 
                SongPassed = Passed;
                Participants = participants;
                BandScore = bandScore;
            }
            public long BandScore;
            public SongData SongData;
            public bool SongPassed;
            public SongParticipantInfo[] Participants;
        }

        public class SongParticipantInfo
        {
            public SupportedInstrument? instrument;
            public SupportedDifficulty Difficulty;
            public int Stars;
            public bool WasGoldStar;
            public bool FC;
            public int Score;
            public float Percentage;
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
            None = 0,
            [Description("Rock Meter")]
            RockMeter = 1,
            [Description("Instant Fail")]
            Fail = 2,
        }
        public enum EnergyLinkType
        {
            [Description("Disabled")]
            None = 0,
            [Description("Check Song")]
            CheckSong = 1,
            [Description("Other Song")]
            OtherSong = 2,
            [Description("Any Song")]
            AnySong = 3,
        }

        public class DeathLinkData
        {
            public DeathLinkData(string source, string cause, DeathLinkType type = DeathLinkType.None) { Source = source; Cause = cause; Type = type; }
            public string Source;
            public string Cause;
            public DeathLinkType Type;
        }
        public class ActionItemData
        {
            public ActionItemData(APActionItem t, string sender)
            {
                type = t;
                Sender = sender;
            }
            public APActionItem type;
            public string Sender;
        }
        public class CurrentlyPlayingData
        {
            public CurrentlyPlayingData(SongData t) { song = t; }
            public SongData song;
            public static CurrentlyPlayingData CurrentlyPlayingSong(SongData t) => new CurrentlyPlayingData(t);
            public static CurrentlyPlayingData CurrentlyPlayingNone() => new CurrentlyPlayingData(null);
        }
        public enum APActionItem
        {
            RockMeterTrap,
            Restart,
            StarPower,
            NonFiller
        }

        public class UserConfig
        {
            public string HOST = "127.0.0.1";
            public int PORT = 26569;
            public string PipeName = "yarg_ap_pipe";
            public bool UsePipe = true;
        }

        public static class Networking
        {
            public class YargAPPacket
            {
                //YARG PARSED
                /// <summary>
                /// Sent to Yarg when a deathlink happens in AP. Causes the current song to fail and exit.
                /// </summary>
                public DeathLinkData deathLinkData = null;
                /// <summary>
                /// Sent to Yarg when an item is recieved that causes yarg to perform an action, such as adding start power or triggering traps
                /// </summary>
                public ActionItemData ActionItem = null;
                /// <summary>
                /// Sent to Yarg to update the game with what songs are available
                /// </summary>
                public (string SongHash, string Profile)[] AvailableSongs = null;

                //AP Parsed
                /// <summary>
                /// Sent to AP Client when a song is completed including whether it passed or failed and what the score was.
                /// </summary>
                public SongCompletedData SongCompletedInfo = null;
                /// <summary>
                /// Sent to AP Client when the currently playing song is changed to update the client title
                /// </summary>
                public CurrentlyPlayingData CurrentlyPlaying = null;

                //DUAL PARSED
                /// <summary>
                /// A log Message. When sent to AP client, prints to the chat log. When sent to Yarg, prints to a toast message
                /// </summary>
                public string Message = null;
            }

            public readonly static Newtonsoft.Json.JsonSerializerSettings PacketSerializeSettings = new Newtonsoft.Json.JsonSerializerSettings()
            {
                Formatting = Newtonsoft.Json.Formatting.None,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            };
        }
    }
}
