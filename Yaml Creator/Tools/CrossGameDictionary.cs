using YargArchipelagoCommon;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static YargArchipelagoCommon.APWorldData;

namespace Yaml_Creator;

public static class CrossGameDictionary
{
    public static void Create()
    {
        var root = CrossPlatformFileLoader.TryGetExistingRoot() ?? DataFolder;
        var cloneHeroPath = Path.Combine(root, "CloneHeroSongExport.json");
        var yargPath = Path.Combine(root, "SongExport.json");
        var outputPath = Path.Combine(root, "CrossGameDictionary.json");

        var cloneHero = Utility.TryParseSongExport(cloneHeroPath, out var cloneHeroError);
        if (cloneHero == null)
        {
            MessageBox.Show($"Could not load the Clone Hero song export.\n\n{cloneHeroError}", "Dictionary Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var yarg = Utility.TryParseSongExport(yargPath, out var yargError);
        if (yarg == null)
        {
            MessageBox.Show($"Could not load the YARG song export.\n\n{yargError}", "Dictionary Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try
        {
            var dictionary = CrossGameDictionary.Create(cloneHero, yarg);
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(dictionary, Formatting.Indented));
            MessageBox.Show("Cross Game Dictionary Created", "Cross-Game Dictionary",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Dictionary Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
    }

    public static IReadOnlyList<SupportedInstrument> SharedInstruments { get; } = Array.AsReadOnly(new[]
    {
        SupportedInstrument.FiveFretGuitar, SupportedInstrument.FiveFretBass, SupportedInstrument.Keys,
        SupportedInstrument.FourLaneDrums, SupportedInstrument.ProDrums, SupportedInstrument.FiveLaneDrums
    });

    public static YargArchipelagoCommon.CrossGameDictionary Create(IEnumerable<SongExportData> cloneHero, IEnumerable<SongExportData> yarg)
    {
        var candidates = new List<(SongExportData CloneHero, SongExportData Yarg, List<SupportedInstrument> Instruments, int MatchingFields)>();
        var yargSongs = yarg.ToArray();
        foreach (var ch in cloneHero)
        {
            if (ch == null || !(ch.Time > 0) || string.IsNullOrWhiteSpace(ch.SongChecksum) || ch.Difficulties == null) continue;
            foreach (var yr in yargSongs)
            {
                if (yr == null || string.IsNullOrWhiteSpace(yr.SongChecksum) || yr.Difficulties == null) continue;
                if (ch.Time != yr.Time || !Same(ch.Name, yr.Name) || !Same(ch.Artist, yr.Artist)) continue;

                var instruments = new List<SupportedInstrument>();
                foreach (var instrument in SharedInstruments)
                {
                    if (ch.Difficulties.TryGetValue(instrument, out var difficulty) && difficulty >= 0 &&
                        yr.Difficulties.TryGetValue(instrument, out var otherDifficulty) && difficulty == otherDifficulty)
                        instruments.Add(instrument);
                }
                if (instruments.Count == 0) continue;

                var matchingFields = 0;
                if (Same(ch.Album, yr.Album)) matchingFields++;
                if (Same(ch.Source, yr.Source)) matchingFields++;
                if (Same(ch.Charter, yr.Charter)) matchingFields++;
                if (Same(ch.Genre, yr.Genre)) matchingFields++;
                candidates.Add((ch, yr, instruments, matchingFields));
            }
        }

        var result = new YargArchipelagoCommon.CrossGameDictionary();
        foreach (var pair in candidates.OrderByDescending(pair => pair.MatchingFields)
            .ThenByDescending(pair => pair.Instruments.Count)
            .ThenBy(pair => pair.CloneHero.SongChecksum).ThenBy(pair => pair.Yarg.SongChecksum))
        {
            var cloneHeroHash = pair.CloneHero.SongChecksum.Trim().ToUpperInvariant();
            var yargHash = pair.Yarg.SongChecksum.Trim();
            if (result.CloneHeroToYarg.ContainsKey(cloneHeroHash) || result.YargToCloneHero.ContainsKey(yargHash)) continue;
            var forward = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var reverse = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var instrument in pair.Instruments)
            {
                forward[instrument.ToString()] = yargHash;
                reverse[instrument.ToString()] = cloneHeroHash;
            }
            result.CloneHeroToYarg.Add(cloneHeroHash, forward);
            result.YargToCloneHero.Add(yargHash, reverse);
        }
        return result;
    }

    internal static bool Same(string left, string right)
    {
        left = Regex.Replace(left ?? "", "<[^>]*>", "").Trim();
        right = Regex.Replace(right ?? "", "<[^>]*>", "").Trim();
        return left.Length > 0 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

