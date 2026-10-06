using YargArchipelagoCommon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static YargArchipelagoCommon.APWorldData;
using CrossGameHashes = YargArchipelagoCommon.CrossGameDictionary;

namespace Yaml_Creator
{
    public static class MissingSongsRepair
    {
        public static void Repair(ClientSession connection)
        {
            if (!connection.ClientConnected || connection.SlotData == null)
            {
                MessageBox.Show("Connect the Tracker Client to the seed you want to repair.", "Song Repair");
                return;
            }
            if (MainForm.ExportFile == null || MainForm.ExportFile.Length == 0)
            {
                MessageBox.Show("Song data has not been loaded.", "Song Repair Failed");
                return;
            }

            try
            {
                var seedFolder = CrossPlatformFileLoader.TryGetSeedsFolder();
                if (seedFolder == null)
                {
                    MessageBox.Show("The seed save folder could not be found.", "Song Repair Failed");
                    return;
                }
                var session = connection.GetSession();
                var player = session.Players.ActivePlayer;
                var savePath = Path.Combine(seedFolder, $"{session.RoomState.Seed}_{player.Slot}_{player.Slot}_{player.GetHashCode()}");
                var save = JObject.Parse(File.ReadAllText(savePath));
                var proxies = save["SongProxies"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                var dictionaryPath = CrossPlatformFileLoader.TryGetExistingFile("CrossGameDictionary.json");
                var dictionary = File.Exists(dictionaryPath)
                    ? JsonConvert.DeserializeObject<CrossGameHashes>(File.ReadAllText(dictionaryPath)) ?? new CrossGameHashes()
                    : new CrossGameHashes();
                var cloneHero = MainForm.Game == "Clone Hero";
                var mappings = cloneHero ? dictionary.YargToCloneHero : dictionary.CloneHeroToYarg;
                var available = new Dictionary<string, SongExportData>(cloneHero ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                foreach (var song in MainForm.ExportFile)
                    if (!string.IsNullOrWhiteSpace(song.core.SongChecksum) && !available.ContainsKey(song.core.SongChecksum))
                        available.Add(song.core.SongChecksum, song.core);
                if (available.Count == 0)
                {
                    MessageBox.Show("The song export contains no usable song hashes.", "Song Repair Failed");
                    return;
                }

                var seedSongs = connection.SlotData.Songs.Cast<BaseAPSong>().Append(connection.SlotData.GoalData).ToArray();
                var usedAnywhere = new HashSet<string>();
                var usedByInstrument = new HashSet<(SupportedInstrument, string)>();
                foreach (var song in seedSongs)
                {
                    var instrument = song.GetPool(connection.SlotData).instrument;
                    var original = FindSong(song.Hash, instrument);
                    var key = song.UniqueKey;
                    if (!proxies.ContainsKey(key) && original != null) key = $"{song.PoolName}[{original.SongChecksum}]";
                    var proxy = proxies.TryGetValue(key, out var hash) ? FindSong(hash, instrument) : null;
                    foreach (var entry in new[] { original, proxy })
                    {
                        if (entry == null) continue;
                        usedAnywhere.Add(entry.SongChecksum);
                        usedByInstrument.Add((instrument, entry.SongChecksum));
                    }
                }

                var report = new List<string>();
                var changed = false;
                foreach (var song in seedSongs)
                {
                    var pool = song.GetPool(connection.SlotData);
                    var original = FindSong(song.Hash, pool.instrument);
                    var key = song.UniqueKey;
                    if (!proxies.ContainsKey(key) && original != null) key = $"{song.PoolName}[{original.SongChecksum}]";
                    if (proxies.TryGetValue(key, out var hash))
                    {
                        if (FindSong(hash, pool.instrument) != null) continue;
                        proxies.Remove(key);
                        changed = true;
                        if (original != null)
                            report.Add($"{song.PoolName}: repalced {original.Name} by {original.Artist}");
                    }
                    if (original != null) continue;

                    var location = session.Locations.GetLocationNameFromId(song.MainLocationID);
                    var oldName = APUtils.GetSongNameFromLocationString(location).Trim();
                    var name = Regex.Replace(location, @"(?: \(\d+\))? Reward [12]$", "");
                    var candidates = available.Values.Where(candidate =>
                        candidate.TryGetDifficulty(pool.instrument, out var difficulty) && difficulty >= 0 &&
                        !usedByInstrument.Contains((pool.instrument, candidate.SongChecksum)))
                        .OrderBy(candidate => usedAnywhere.Contains(candidate.SongChecksum)).ToArray();
                    var replacement = candidates.FirstOrDefault(candidate => CrossGameDictionary.Same(name,
                        $"{candidate.Name} by {candidate.Artist} on {pool.instrument.GetDescription()}"));
                    if (replacement == null)
                        replacement = candidates.FirstOrDefault(candidate => candidate.Time > 0 && candidate.ValidForPool(pool));
                    if (replacement == null)
                    {
                        report.Add($"{song.PoolName}: No replacement found for {oldName}");
                        continue;
                    }
                    proxies[key] = replacement.SongChecksum;
                    usedAnywhere.Add(replacement.SongChecksum);
                    usedByInstrument.Add((pool.instrument, replacement.SongChecksum));
                    report.Add($"{song.PoolName}: {oldName} -> {replacement.Name} by {replacement.Artist}");
                    changed = true;
                }

                if (changed)
                {
                    save["SongProxies"] = JObject.FromObject(proxies);
                    File.WriteAllText(savePath, save.ToString(Formatting.Indented));
                }
                if (report.Count == 0) report.Add("No missing songs found.");
                MessageBox.Show(string.Join(Environment.NewLine, report), "Song Repair");

                SongExportData FindSong(string hash, SupportedInstrument instrument)
                {
                    if (hash == null) return null;
                    if (available.TryGetValue(hash, out var song)) return song;
                    if (mappings.TryGetValue(cloneHero ? hash : hash.ToUpperInvariant(), out var instruments) &&
                        instruments.TryGetValue(instrument.ToString(), out var mappedHash) && available.TryGetValue(mappedHash, out song))
                        return song;
                    return null;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Song Repair Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
}
