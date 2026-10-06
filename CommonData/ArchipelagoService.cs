using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using BepInEx.Logging;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if !CLONE_HERO
using YARG.Core.Song;
using YARG.Gameplay;
#endif
using YargArchipelagoCommon;

namespace YargArchipelagoCommon
{
    public class APConnectionContainer : BaseConnectionContainer
    {
        public DeathLinkService DeathLinkService { get; private set; } = null;
        public override bool IsSessionConnected => !IsConnecting && SlotData != null && ClientConnected;
        public bool IsConnecting { get; private set; }
        public string StatusText { get; private set; } = "Disconnected";
        public Dictionary<string, SongEntry> SongHashLookup { get; private set; } = new Dictionary<string, SongEntry>(StringComparer.OrdinalIgnoreCase);
        public CrossGameDictionary CrossGameHashes { get; private set; } = new CrossGameDictionary();
        public HashSet<long> CheckedLocations { get; } = new HashSet<long>();
        private DateTime CurrentSongStartTime = DateTime.Now;
        private GameManager CurrentlyPlaying = null;
        private readonly ArchipelagoEventManager eventManager;
        internal readonly ConcurrentQueue<Action> MainThreadActions = new();
        private readonly Action<ArchipelagoEventManager> AddGameListeners;
        private readonly Action<ArchipelagoEventManager> RemoveGameListeners;
        private int ConnectionAttempt;
        private ReceivedItemsHelper.ItemReceivedHandler ItemListener;
        private LocationCheckHelper.CheckedLocationsUpdatedHandler LocationListener;
        private MessageLogHelper.MessageReceivedHandler MessageListener;
        private DeathLinkService.DeathLinkReceivedHandler DeathLinkListener;

        public APConnectionContainer(ManualLogSource logSource, Action<ArchipelagoEventManager> addListeners, Action<ArchipelagoEventManager> removeListeners)
        {
            LogInfo = logSource.LogInfo;
            LogWarning = logSource.LogWarning;
            LogError = logSource.LogError;
            eventManager = new ArchipelagoEventManager(this);
            AddGameListeners = addListeners;
            RemoveGameListeners = removeListeners;
        }

        public void Connect(ConnectionDetails connectionDetails, bool skipVersionCheck = false)
        {
            if (IsConnecting || IsSessionConnected)
                return;
            if (connectionDetails == null || string.IsNullOrWhiteSpace(connectionDetails.SlotName))
            {
                Report("Enter a slot name.");
                return;
            }

            if (!ConnectionDetails.TryParseAddress(connectionDetails.Address, out var address, out var error))
            {
                Report(error);
                return;
            }

            if (Session != null)
                Disconnect(false);
            var details = new ConnectionDetails
            {
                Address = connectionDetails.Address.Trim(),
                SlotName = connectionDetails.SlotName.Trim(),
                Password = connectionDetails.Password
            };
            var attempt = ++ConnectionAttempt;
            IsConnecting = true;
            StatusText = "Connecting...";
            _ = Task.Run(() => TryConnectAsync(details, address, attempt, skipVersionCheck));
        }

        private void TryConnectAsync(ConnectionDetails details, Uri address, int attempt, bool skipVersionCheck)
        {
            ArchipelagoSession tempSession = null;
            try
            {
                tempSession = ArchipelagoSessionFactory.CreateSession(address);
                var result = tempSession.TryConnectAndLogin(APWorldData.Game, details.SlotName,
                    ItemsHandlingFlags.AllItems, Version.Parse(Versions.Archipelago), password: details.Password);
                if (result is LoginFailure failure)
                {
                    FailConnection(attempt, tempSession, string.Join("\n", failure.Errors));
                    return;
                }

                var slotData = APSlotData.Parse(tempSession.DataStorage.GetSlotData());
                var ClientVersion = Version.Parse(Versions.APWorld);
                if (!skipVersionCheck && (slotData.APWorldVersion.Major != ClientVersion.Major ||
                    slotData.APWorldVersion.Minor != ClientVersion.Minor))
                {
                    FailConnection(attempt, tempSession, $"Version mismatch: world {slotData.APWorldVersion}, client supports {ClientVersion.Major}.{ClientVersion.Minor}.x");
                    return;
                }

                MainThreadActions.Enqueue(() => FinishConnection(attempt, tempSession, slotData, details));
            }
            catch (Exception e)
            {
                FailConnection(attempt, tempSession, e.Message);
            }
        }

        private void FailConnection(int attempt, ArchipelagoSession session, string message)
        {
            CloseSocket(session);
            MainThreadActions.Enqueue(() =>
            {
                if (attempt != ConnectionAttempt)
                    return;
                IsConnecting = false;
                Report($"Failed to connect: {message}");
            });
        }

        private void FinishConnection(int attempt, ArchipelagoSession session, APSlotData slotData, ConnectionDetails details)
        {
            if (attempt != ConnectionAttempt)
            {
                CloseSocket(session);
                return;
            }
            try
            {
                if (!session.Socket.Connected)
                {
                    FailConnection(attempt, session, "The server disconnected during login.");
                    return;
                }
                Session = session;
                SlotData = slotData;
                IsConnecting = false;
                seedConfig = PersistantData.Load(this, ArchipelagoPlugin.DefaultItemLog.Value, ArchipelagoPlugin.DefaultShowChat.Value);
                DeathLinkService = session.CreateDeathLinkService();
                AddListeners();
                UpdateDeathLinkTags();
                UpdateReceivedItems();
                BuildSongLookup();
                ReadCrossGameDict();
                CheckForMissingSongs();
                details.Save();
                OnConnected(details);
            }
            catch (Exception e)
            {
                Disconnect(false);
                Report($"Failed to initialize connection: {e.Message}");
            }
        }

        private void OnConnected(ConnectionDetails details)
        {
            File.WriteAllText(Path.Combine(APWorldData.DataFolder, "Debug.json"), JsonConvert.SerializeObject(SlotData, Formatting.Indented));
            StatusText = $"Connected: {details.SlotName}@{details.Address}";
            APToastManager.ToastSuccess($"Connected Archipelago!\n{details.SlotName}@{details.Address}");
        }

        public void Disconnect(bool showNotification = true)
        {
            ConnectionAttempt++;
            IsConnecting = false;
            RemoveListeners();
            CloseSocket(Session);
            DeathLinkService = null;
            Session = null;
            SlotData = null;
            seedConfig = null;
            ReceivedSongUnlockItems.Clear();
            ReceivedInstruments.Clear();
            ApItemsRecieved.Clear();
            ItemPriorities.Clear();
            CheckedLocations.Clear();
            SongHashLookup.Clear();
            ResetConnectionData();
            StatusText = "Disconnected";
            APPatches.HasAvailableAPSongUpdate = true;
            if (showNotification)
                Report("Disconnected from Archipelago");
        }

        private void ResetConnectionData()
        {
            eventManager.PendingTrapsFiller = false;
        }

        private void CloseSocket(ArchipelagoSession session)
        {
            try
            {
                if (session?.Socket != null)
                    _ = session.Socket.DisconnectAsync();
            }
            catch (Exception e) { LogWarning?.Invoke($"Could not close Archipelago socket: {e.Message}"); }
        }

        private void AddListeners()
        {
            var session = Session;
            ItemListener = _ => QueueSessionAction(session, UpdateReceivedItems);
            LocationListener = _ => QueueSessionAction(session, UpdateReceivedItems);
            MessageListener = message => QueueSessionAction(session, () => OnMessageReceived(message));
            DeathLinkListener = deathLink => QueueSessionAction(session, () => eventManager.OnDeathLinkReceived(deathLink));
            DeathLinkService.OnDeathLinkReceived += DeathLinkListener;
            session.Hints.TrackHints(_ => QueueSessionAction(session, () => APPatches.HasAvailableAPSongUpdate = true));
            session.Items.ItemReceived += ItemListener;
            session.Locations.CheckedLocationsUpdated += LocationListener;
            session.MessageLog.OnMessageReceived += MessageListener;

            AddGameListeners(eventManager);
        }

        private void RemoveListeners()
        {
            RemoveGameListeners(eventManager);
            ClearCurrentSong();
            if (DeathLinkService != null) DeathLinkService.OnDeathLinkReceived -= DeathLinkListener;
            if (Session == null)
                return;
            Session.Items.ItemReceived -= ItemListener;
            Session.Locations.CheckedLocationsUpdated -= LocationListener;
            Session.MessageLog.OnMessageReceived -= MessageListener;
        }

        public void TickMainThread()
        {
            while (MainThreadActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { LogError?.Invoke($"Archipelago update failed: {e}"); }
            }
            if (Session != null && !ClientConnected)
            {
                Disconnect(false);
                Report("Lost connection to the Archipelago server");
            }
        }

        private void QueueSessionAction(ArchipelagoSession session, Action action)
        {
            MainThreadActions.Enqueue(() =>
            {
                if (ReferenceEquals(Session, session))
                    action();
            });
        }

        public void SetCurrentSong(GameManager game)
        {
            CurrentSongStartTime = DateTime.Now;
            CurrentlyPlaying = game;
        }

        public void ResetBuffer() => CurrentSongStartTime = DateTime.Now;

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

        public void BuildSongLookup()
        {
            SongHashLookup.Clear();
            foreach(var song in EngineActions.GetSongLookup())
                SongHashLookup[song.Key] = song.Value;
        }

        public List<SongAPData> GetAvailableSongs(Func<SongEntry, bool> Predicate = null) => GetAvailableSongs(Predicate, out _, out _);

        public List<SongAPData> GetAvailableSongs(Func<SongEntry, bool> Predicate, out List<SongAPData> MissingInstrument, out List<SongAPData> AllAvailable)
        {
            MissingInstrument = new List<SongAPData>();
            AllAvailable = new List<SongAPData>();
            List<SongAPData> SongEntries = new List<SongAPData>();
            if (!IsSessionConnected)
                return SongEntries;
            foreach (var i in SlotData.SongsByInstrument)
            {
                var HasInstrument = ReceivedInstruments.ContainsKey(i.Key);
                foreach (var song in i.Value)
                {
                    if (!ReceivedSongUnlockItems.ContainsKey(song.UnlockItemID)) continue;
                    if (!song.HasAvailableLocations(this)) continue;
                    if (!SongHashLookup.TryGetValue(song.GetActiveHash(this), out var entry)) continue;
                    if (Predicate != null && !Predicate(entry)) continue;
                    AllAvailable.Add(song);
                    if (HasInstrument) SongEntries.Add(song);
                    else MissingInstrument.Add(song);
                }
            }
            return SongEntries;
        }

        public void ReadCrossGameDict()
        {
            var CrossgameFile = Path.Combine(APWorldData.DataFolder, "CrossGameDictionary.json");
            try
            {
                CrossGameHashes = JsonConvert.DeserializeObject<CrossGameDictionary>(File.ReadAllText(CrossgameFile));
            }
            catch (Exception e)
            {
                LogWarning?.Invoke($"Failed to load CrossGameDictionary.json \n{e.Message}");
            }
        }

        public string TryCorrectHash(BaseAPSong song) => TryCorrectHash(song.Hash, song.GetPool(SlotData).instrument.ToString());

        public string TryCorrectHash(string hash, string instrument)
        {
#if CLONE_HERO
            var hashes = CrossGameHashes?.YargToCloneHero;
#else
            var hashes = CrossGameHashes?.CloneHeroToYarg;
#endif
            if (!SongHashLookup.ContainsKey(hash) && hashes != null &&
                hashes.TryGetValue(hash, out var instruments) &&
                instruments.TryGetValue(instrument, out var replacement) && SongHashLookup.ContainsKey(replacement))
                return replacement;
            return hash;
        }

        private void CheckForMissingSongs()
        {
            SlotData.GoalData.Hash = TryCorrectHash(SlotData.GoalData);

            foreach (var song in SlotData.Songs)
                song.Hash = TryCorrectHash(song);

            foreach (var key in seedConfig.SongProxies.Keys.Concat(seedConfig.AdjustedDifficulties.Keys).Distinct().ToArray())
            {
                var separator = key.LastIndexOf('[');
                if (separator < 0 || !key.EndsWith("]") || !SlotData.Pools.TryGetValue(key.Substring(0, separator), out var pool)) continue;
                var instrument = pool.instrument.ToString();
                var hash = key.Substring(separator + 1, key.Length - separator - 2);
                var replacementKey = $"{key.Substring(0, separator)}[{TryCorrectHash(hash, instrument)}]";

                if (seedConfig.SongProxies.TryGetValue(key, out var proxyHash))
                {
                    seedConfig.SongProxies.Remove(key);
                    seedConfig.SongProxies[replacementKey] = TryCorrectHash(proxyHash, instrument);
                }
                if (key != replacementKey && seedConfig.AdjustedDifficulties.TryGetValue(key, out var requirements))
                {
                    seedConfig.AdjustedDifficulties.Remove(key);
                    seedConfig.AdjustedDifficulties[replacementKey] = requirements;
                }
            }

            var invalidProxies = seedConfig.SongProxies.Where(proxy => !SongHashLookup.ContainsKey(proxy.Value)).Select(proxy => proxy.Key).ToArray();
            foreach (var key in invalidProxies)
                seedConfig.SongProxies.Remove(key);
            if (invalidProxies.Length > 0)
                seedConfig.Save();

            HashSet<string> Missing = new HashSet<string>();
            foreach(var i in SlotData.Songs)
            {
                if (!SongHashLookup.ContainsKey(i.GetActiveHash(this)))
                {
                    var MainLocation = Session.Locations.GetLocationNameFromId(i.MainLocationID);
                    var SongName = APUtils.GetSongNameFromLocationString(MainLocation);
                    Missing.Add(SongName);
                }
            }
            if (Missing.Count > 0)
            {
                ArchipelagoConnectionDialog.ShowMissingSongs(Missing);
            }
        }

        public new void UpdateReceivedItems()
        {
            if (Session == null || SlotData == null)
                return;
            ReceivedSongUnlockItems.Clear();
            ReceivedInstruments.Clear();
            ApItemsRecieved.Clear();
            ItemPriorities.Clear();
            base.UpdateReceivedItems();
            UpdateCheckedLocations();
            eventManager.PendingTrapsFiller = true;
            APPatches.HasAvailableAPSongUpdate = true;
            if (ApItemsRecieved.Any(x => x.Type == StaticItems.Victory))
                Session.SetGoalAchieved();
        }

        public void UpdateCheckedLocations()
        {
            CheckedLocations.Clear();
            CheckedLocations.UnionWith(Session.Locations.AllLocationsChecked);
        }

        public void UpdateDeathLinkTags()
        {
            if (!IsSessionConnected || DeathLinkService == null) return;
            if (seedConfig.DeathLinkMode > DeathLinkType.disabled)
                DeathLinkService.EnableDeathLink();
            else
                DeathLinkService.DisableDeathLink();
        }

        private void OnMessageReceived(LogMessage message)
        {
            eventManager.RelayChat(message);
            eventManager.UpdateChatHistory(message);
        }

        private void Report(string message)
        {
            StatusText = message;
            APToastManager.ToastInformation(message);
        }
    }

}
