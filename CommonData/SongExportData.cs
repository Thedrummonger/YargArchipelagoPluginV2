using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace YargArchipelagoCommon
{
    public class SongExportData
    {
        public string Name;
        public string Artist;
        public string SongChecksum;
        public string Source;
        public string Album;
        public string Genre;
        public string Charter;
        public double Time;
        public Dictionary<SupportedInstrument, int> Difficulties = new Dictionary<SupportedInstrument, int>();

        public bool TryGetDifficulty(SupportedInstrument instrument, out int Difficulty) => Difficulties.TryGetValue(instrument, out Difficulty);
        public bool ValidForPool(SongPool pool) => TryGetDifficulty(pool.instrument, out var diff) &&
            diff <= pool.max_difficulty && diff >= pool.min_difficulty &&
            Time <= pool.max_time && Time >= pool.min_time;

        public void AddIntensity(SupportedInstrument instrument, int intensity)
        {
            if (intensity >= 0)
                Difficulties[instrument] = intensity;
        }

        public static void WriteToFile(string file, IEnumerable<SongExportData> songs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonConvert.SerializeObject(songs, Formatting.Indented));
        }
    }
}
