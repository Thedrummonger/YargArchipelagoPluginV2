using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Yaml_Creator.SongData;
using static Yaml_Creator.SongDataConverter;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public sealed class SongPoolConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Instrument { get; set; } = string.Empty;
        public int AmountInPool { get; set; }
        public int MinDifficulty { get; set; }
        public int MaxDifficulty { get; set; }
        public int MinTime { get; set; }
        public int MaxTime { get; set; }
    }

    public sealed class SongDistributionResult
    {
        public Dictionary<string, List<string>> PoolAssignments { get; set; } =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
    }

    public sealed class SongDistributor
    {
        private readonly Random _random = new Random();

        private readonly Dictionary<string, HashSet<string>> _assignedSongInstruments =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<string>> _poolAssignments =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private bool _goalPlaced;

        private Dictionary<string, CompressedSongData> _availableSongs =
            new Dictionary<string, CompressedSongData>(StringComparer.Ordinal);

        private SongExportExtendedData[] _rawAvailableSongs = new SongExportExtendedData[0];

        private Dictionary<string, YAMLSongPool> _songPools =
            new Dictionary<string, YAMLSongPool>(StringComparer.Ordinal);

        private bool _reuseSongsAcrossInstruments;

        private Dictionary<string, List<string>> _inclusionLists =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private Dictionary<string, List<string>> _exclusionLists =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private string _goalSong;
        private string _goalPool;

        public SongDistributor()
        {

        }

        public SongDistributor WithAvailableSongs(IEnumerable<SongExportExtendedData> availableSongs)
        {
            _rawAvailableSongs = availableSongs.ToArray();
            _availableSongs = availableSongs.ToDictionary(x => x.core.SongChecksum, x => x.Compress());
            return this;
        }

        public SongDistributor WithPools(Dictionary<string, YAMLSongPool> songPools)
        {
            _songPools = songPools ?? throw new ArgumentNullException(nameof(songPools));
            return this;
        }

        public SongDistributor WithReuseSongs(bool reuse)
        {
            _reuseSongsAcrossInstruments = reuse;
            return this;
        }

        // Distibuter (_inclusionLists) is key: pool, value: list of song hashes
        // Yaml (whats being passed to this function) is Key: Song Hash, Value: Pool list
        public SongDistributor WithInclusionLists(Dictionary<string, string[]> inclusionLists)
        {
            _inclusionLists = InvertSongToPoolsMap(inclusionLists);
            return this;
        }

        public SongDistributor WithExclusionLists(Dictionary<string, string[]> exclusionLists)
        {
            _exclusionLists = InvertSongToPoolsMap(exclusionLists);
            return this;
        }

        private static Dictionary<string, List<string>> InvertSongToPoolsMap(Dictionary<string, string[]> source)
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            foreach (var pair in source)
                foreach (var i in pair.Value)
                {
                    if (!result.ContainsKey(i)) 
                        result.Add(i, new List<string>());
                    result[i].Add(pair.Key);
                }
            return result;
        }

        public SongDistributor WithGoalSong(string goalSong, string goalPool = null)
        {
            _goalSong = goalSong;
            _goalPool = goalPool;
            return this;
        }

        public bool CreateTrimmedSetlistforYAML(out SongExportExtendedData[] Setlist)
        {
            Setlist = new SongExportExtendedData[0];
            SongDistributionResult Result;
            try
            {
                Result = Distribute();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to gen setlist\n{ex.Message}");
                return false;
            }
            HashSet<string> AllUsedSongs = new HashSet<string>();
            foreach (var songlist in Result.PoolAssignments.Values)
                foreach (var song in songlist)
                    AllUsedSongs.Add(song);

            foreach (var includeSongs in _inclusionLists.Values)
                foreach (var song in includeSongs)
                    AllUsedSongs.Add(song);

            if (!string.IsNullOrWhiteSpace(_goalSong))
                AllUsedSongs.Add(_goalSong);

            List<SongExportExtendedData> FinalSetlist = new List<SongExportExtendedData>();
            foreach (var i in AllUsedSongs)
            {
                var songData = _rawAvailableSongs.First(x => x.core.SongChecksum == i);
                FinalSetlist.Add(songData);
            }
            Setlist = FinalSetlist.ToArray();
            return true;
        }

        public SongDistributionResult Distribute()
        {
            var pools = ParsePools(_songPools);

            ProcessGoalSong(pools);

            var sortedPools = SortPools(pools);

            foreach (var pool in sortedPools)
                ProcessInclusionList(pool);

            foreach (var pool in sortedPools)
                AssignSongsToPool(pool);

            BackfillShortages(sortedPools);

            if (!string.IsNullOrEmpty(_goalSong) && !_goalPlaced)
                throw new Exception($"Could not place goal song '{_goalSong}' in any pool");

            return new SongDistributionResult
            {
                PoolAssignments = new Dictionary<string, List<string>>(_poolAssignments, StringComparer.Ordinal)
            };
        }

        private static List<SongPoolConfig> ParsePools(Dictionary<string, YAMLSongPool> poolsDict)
        {
            var result = new List<SongPoolConfig>();

            foreach (var kvp in poolsDict)
            {
                result.Add(new SongPoolConfig
                {
                    Name = kvp.Key,
                    Instrument = kvp.Value.instrument.ToString(),
                    AmountInPool = (int)kvp.Value.amount_in_pool + kvp.Value.random_variance,
                    MinDifficulty = (int)kvp.Value.min_difficulty,
                    MaxDifficulty = (int)kvp.Value.max_difficulty,
                    MinTime = (int)kvp.Value.min_time,
                    MaxTime = (int)kvp.Value.max_time,
                });
            }

            return result;
        }

        private static List<SongPoolConfig> SortPools(List<SongPoolConfig> pools)
        {
            return pools
                .Where(p => p.AmountInPool > 0)
                .OrderBy(p => p.MaxDifficulty - p.MinDifficulty)
                .ThenByDescending(p => p.AmountInPool)
                .ToList();
        }

        /// <summary>
        /// Hard check: a song can never appear in more than one pool with the same instrument.
        /// </summary>
        private bool SongWouldCreateDuplicateInstrument(string songHash, SongPoolConfig pool)
        {
            return _assignedSongInstruments.TryGetValue(songHash, out var instruments)
                   && instruments.Contains(pool.Instrument);
        }

        private bool SongAlreadyUsed(string songHash, SongPoolConfig pool)
        {
            if (SongWouldCreateDuplicateInstrument(songHash, pool))
                return true;

            if (!_reuseSongsAcrossInstruments && _assignedSongInstruments.ContainsKey(songHash))
                return true;

            return false;
        }

        private void ProcessGoalSong(List<SongPoolConfig> pools)
        {
            if (string.IsNullOrEmpty(_goalSong) || _goalPlaced)
                return;

            if (!_availableSongs.TryGetValue(_goalSong, out var songData))
                throw new Exception($"Goal song '{_goalSong}' is not in the available songs");

            if (!string.IsNullOrEmpty(_goalPool))
            {
                var targetPool = pools.FirstOrDefault(p => string.Equals(p.Name, _goalPool, StringComparison.Ordinal));
                if (targetPool == null)
                    throw new Exception($"Goal pool '{_goalPool}' does not exist in song pools");

                if (!songData.Difficulties.ContainsKey(targetPool.Instrument))
                {
                    throw new Exception(
                        $"Goal song '{_goalSong}' does not have instrument '{targetPool.Instrument}' " +
                        $"required by goal pool '{_goalPool}'");
                }

                AssignSongToPoolInternal(_goalSong, targetPool);
                _goalPlaced = true;
            }
            else
            {
                var shuffledPools = new List<SongPoolConfig>(pools);
                Shuffle(shuffledPools);

                foreach (var pool in shuffledPools)
                {
                    if (songData.Difficulties.ContainsKey(pool.Instrument) &&
                        !SongWouldCreateDuplicateInstrument(_goalSong, pool))
                    {
                        AssignSongToPoolInternal(_goalSong, pool);
                        _goalPlaced = true;
                        return;
                    }
                }
            }
        }

        private void ProcessInclusionList(SongPoolConfig pool)
        {
            if (!_inclusionLists.TryGetValue(pool.Name, out var includedSongs))
                return;

            var uniqueIncludedSongs = includedSongs.Distinct(StringComparer.Ordinal).ToList();

            var currentCount = _poolAssignments.TryGetValue(pool.Name, out var assigned)
                ? assigned.Count
                : 0;

            var songsToInclude = Math.Min(uniqueIncludedSongs.Count, pool.AmountInPool - currentCount);

            for (int i = 0; i < songsToInclude; i++)
            {
                var songHash = uniqueIncludedSongs[i];

                if (!_availableSongs.TryGetValue(songHash, out var songData))
                    throw new Exception($"Inclusion list for pool '{pool.Name}' contains unknown song hash: {songHash}");

                if (!songData.Difficulties.ContainsKey(pool.Instrument))
                {
                    throw new Exception(
                        $"Inclusion list for pool '{pool.Name}' contains song '{songHash}' " +
                        $"which does not have instrument '{pool.Instrument}'");
                }

                if (SongWouldCreateDuplicateInstrument(songHash, pool))
                    continue;

                AssignSongToPoolInternal(songHash, pool);
            }
        }

        private void AssignSongsToPool(SongPoolConfig pool)
        {
            var exclusionSet = _exclusionLists.TryGetValue(pool.Name, out var exclusions)
                ? new HashSet<string>(exclusions, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            var eligible = _availableSongs.Keys
                .Where(songHash =>
                    !SongAlreadyUsed(songHash, pool) &&
                    !exclusionSet.Contains(songHash) &&
                    SongFitsPool(songHash, pool))
                .ToList();

            Shuffle(eligible);

            var currentCount = _poolAssignments.TryGetValue(pool.Name, out var assigned)
                ? assigned.Count
                : 0;

            var needed = pool.AmountInPool - currentCount;

            List<string> selected;
            if (eligible.Count < needed)
            {
                Console.WriteLine(
                    $"Pool '{pool.Name}' ({pool.Instrument}, difficulty {pool.MinDifficulty}-{pool.MaxDifficulty}): " +
                    $"Requested {needed} more songs but only {eligible.Count} eligible songs available");

                selected = eligible;
            }
            else
            {
                selected = eligible.Take(needed).ToList();
            }

            foreach (var songHash in selected)
                AssignSongToPoolInternal(songHash, pool);
        }

        private void BackfillShortages(List<SongPoolConfig> pools)
        {
            foreach (var pool in pools)
            {
                while (GetPoolCount(pool.Name) < pool.AmountInPool)
                {
                    var foundDonor = TryStealOneSong(pool, pools);

                    if (!foundDonor)
                    {
                        var currentCount = GetPoolCount(pool.Name);
                        throw new Exception(
                            $"Pool '{pool.Name}' ({pool.Instrument}, difficulty {pool.MinDifficulty}-{pool.MaxDifficulty}, Length {pool.MinTime}-{pool.MaxTime}): " +
                            $"Cannot fulfill request for {pool.AmountInPool} songs. Only {currentCount} songs available after backfilling. " +
                            $"Please reduce amount_in_pool, expand difficulty range, or ensure more songs are available.");
                    }
                }
            }
        }

        private bool TryStealOneSong(SongPoolConfig recipientPool, List<SongPoolConfig> allPools)
        {
            foreach (var donorPool in allPools)
            {
                if (string.Equals(donorPool.Name, recipientPool.Name, StringComparison.Ordinal))
                    continue;

                var donorCurrent = GetPoolCount(donorPool.Name);
                if (donorCurrent < donorPool.AmountInPool)
                    continue;

                if (!_poolAssignments.TryGetValue(donorPool.Name, out var donorSongHashes) || donorSongHashes.Count == 0)
                    continue;

                foreach (var songHash in donorSongHashes.ToList())
                {
                    // Never steal the goal song out of the goal pool if both are assigned.
                    if (!string.IsNullOrEmpty(_goalSong) &&
                        !string.IsNullOrEmpty(_goalPool) &&
                        string.Equals(_goalSong, songHash, StringComparison.Ordinal) &&
                        string.Equals(_goalPool, donorPool.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Don't steal songs from pools they were plando'd to.
                    if (_inclusionLists.TryGetValue(donorPool.Name, out var donorInclusions) &&
                        donorInclusions.Contains(songHash, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    if (!SongFitsPool(songHash, recipientPool))
                        continue;

                    // If donor and recipient instruments differ, check duplicate-instrument safety.
                    if (!string.Equals(donorPool.Instrument, recipientPool.Instrument, StringComparison.Ordinal) &&
                        SongWouldCreateDuplicateInstrument(songHash, recipientPool))
                    {
                        continue;
                    }

                    var refillSong = FindRefillForDonor(donorPool);
                    if (refillSong == null)
                        continue;

                    RemoveSongFromPoolInternal(songHash, donorPool);
                    AssignSongToPoolInternal(refillSong, donorPool);
                    AssignSongToPoolInternal(songHash, recipientPool);

                    return true;
                }
            }

            return false;
        }

        private bool SongFitsPool(string songHash, SongPoolConfig pool)
        {
            var songData = _availableSongs[songHash];

            return songData.Difficulties.TryGetValue(pool.Instrument, out var difficulty) &&
                   difficulty >= pool.MinDifficulty &&
                   difficulty <= pool.MaxDifficulty &&
                   songData.Time >= pool.MinTime &&
                   songData.Time <= pool.MaxTime;
        }

        private string FindRefillForDonor(SongPoolConfig donorPool)
        {
            var exclusionSet = _exclusionLists.TryGetValue(donorPool.Name, out var exclusions)
                ? new HashSet<string>(exclusions, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            var candidates = _availableSongs.Keys
                .Where(songHash =>
                    !SongAlreadyUsed(songHash, donorPool) &&
                    !exclusionSet.Contains(songHash) &&
                    SongFitsPool(songHash, donorPool))
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates[_random.Next(candidates.Count)];
        }

        private void AssignSongToPoolInternal(string songHash, SongPoolConfig pool)
        {
            if (!_poolAssignments.TryGetValue(pool.Name, out var songs))
            {
                songs = new List<string>();
                _poolAssignments[pool.Name] = songs;
            }

            songs.Add(songHash);

            if (!_assignedSongInstruments.TryGetValue(songHash, out var instruments))
            {
                instruments = new HashSet<string>(StringComparer.Ordinal);
                _assignedSongInstruments[songHash] = instruments;
            }

            instruments.Add(pool.Instrument);
        }

        private void RemoveSongFromPoolInternal(string songHash, SongPoolConfig pool)
        {
            _poolAssignments[pool.Name].Remove(songHash);

            var instruments = _assignedSongInstruments[songHash];
            instruments.Remove(pool.Instrument);

            if (instruments.Count == 0)
                _assignedSongInstruments.Remove(songHash);
        }

        private int GetPoolCount(string poolName)
        {
            return _poolAssignments.TryGetValue(poolName, out var songs) ? songs.Count : 0;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

