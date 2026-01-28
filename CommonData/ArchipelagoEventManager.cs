using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using YARG.Core;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Settings;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public class ArchipelagoEventManager
    {
        public ArchipelagoEventManager (APConnectionContainer connectionContainer) { parent = connectionContainer; }
        APConnectionContainer parent;

        public void Items_ItemReceived(Archipelago.MultiClient.Net.Helpers.ReceivedItemsHelper _) => 
            parent.APSyncTimer.FlagUpdate();
        public void Locations_CheckedLocationsUpdated(System.Collections.ObjectModel.ReadOnlyCollection<long> _) => 
            parent.APSyncTimer.FlagUpdate();
        public void InsertAPSongs(MusicLibraryMenu __instance, List<ViewType> __result)
        {
            if (!parent.IsSessionConnected)
                return;
            List<(SongEntry, SongAPData)> SongEntries = new List<(SongEntry, SongAPData)>();
            foreach (var i in parent.SlotData.SongsByInstrument)
            {
                if (!parent.ReceivedInstruments.ContainsKey(i.Key)) continue;
                foreach (var song in i.Value)
                {
                    if (!parent.ReceivedSongUnlockItems.ContainsKey(song.UnlockItemID)) continue;
                    if (!song.HasAvailableLocations(parent)) continue;
                    var songObj = song.GetYargSongEntry(parent);
                    if (songObj != null)
                        SongEntries.Add((songObj, song));
                }
            }
            YargEngineActions.InsertAPListViewSongs(parent, __instance, __result, SongEntries);
        }

        public void SetSong(GameManager gameManager) => parent.SetCurrentSong(gameManager);
        public void SetSong() => parent.ClearCurrentSong();

        public void FailedSong(GameManager gameManager)
        {
            if (!parent.IsSessionConnected)
                return;
#if NIGHTLY
            if (SettingsManager.Settings.NoFailMode.Value && gameManager.IsPractice && gameManager.PlayerHasFailed) return;
#endif
            if ((parent.seedConfig?.DeathLinkMode ?? DeathLinkType.disabled) > DeathLinkType.disabled)
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.LastUsedConnectionInfo?.SlotName,
                    $"Failed Song {gameManager.Song.Name} by {gameManager.Song.Artist}"));
        }

        public void TryCheckSongLocations(GameManager gameManager)
        {
            bool ShouldCheat = APPatches.IgnoreScoreForNextSong;
            APPatches.IgnoreScoreForNextSong = false;
            if (!parent.IsSessionConnected)
                return;
            var LocationsPlayed = parent.SlotData.Songs.Where(x => x.WasActiveSongInGame(parent, gameManager));
            var DoDeathlink = false;
            List<long> LocationsToComplete = new List<long>();
            foreach (var i in LocationsPlayed)
            {
                if (!i.IsSongUnlocked(parent))
                    continue;
                if (!i.HasAvailableLocations(parent))
                    continue;
                var pool = i.GetPool(parent.SlotData);

                var MetStandard = pool.MetStandard(gameManager, out var deathLinkStandard, i.GetCurrentCompletionRequirements(parent)) || ShouldCheat;
                if (!MetStandard && deathLinkStandard) DoDeathlink = true;
                if (MetStandard) LocationsToComplete.Add(i.MainLocationID);

                var MetExtra = true;
                if (i.ExtraLocationID >= 0)
                {
                    MetExtra = pool.MetExtra(gameManager, out var deathLinkExtra, i.GetCurrentCompletionRequirements(parent)) || ShouldCheat;
                    if (!MetExtra && deathLinkExtra) DoDeathlink = true;
                    if (MetExtra) LocationsToComplete.Add(i.ExtraLocationID);
                }

                if (i.CompletionLocationID >= 0 && MetStandard && MetExtra)
                    LocationsToComplete.Add(i.CompletionLocationID);
            }

            // Filter out locations that are already check, specifically for energy link option checking
            LocationsToComplete = LocationsToComplete.Where(x => !parent.GetSession().Locations.AllLocationsChecked.Contains(x)).ToList();
            var HasLocationsTocheck = LocationsToComplete.Count > 0;

            if (HasLocationsTocheck)
                parent.GetSession().Locations.CompleteLocationChecks(LocationsToComplete.ToArray());

            ExtraAPFunctionalityHelper.SendScoreAsEnergy(parent, gameManager.BandScore, HasLocationsTocheck);

            if (DoDeathlink && (parent.seedConfig?.DeathLinkMode ?? DeathLinkType.disabled) > DeathLinkType.disabled)
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.LastUsedConnectionInfo?.SlotName,
                    $"Failed to meet the requirements playing {gameManager.Song.Name} by {gameManager.Song.Artist}"));
        }
        internal void TryCheckSongGoalSong(GameManager manager)
        {
            if (!parent.IsSessionConnected)
                return;
            if (!parent.SlotData.GoalData.WasActiveSongInGame(parent, manager))
                return;
            if (!parent.SlotData.GoalData.IsSongUnlocked(parent))
                return;

            var pool = parent.SlotData.GoalData.GetPool(parent.SlotData);
            var MetStandard = pool.MetStandard(manager, out var deathLinkStandard);
            var MetExtra = pool.MetExtra(manager, out var deathLinkExtra);

            if (MetStandard && MetExtra)
                parent.GetSession().Locations.CompleteLocationChecks(parent.SlotData.GoalData.MainLocationID);

            if ((deathLinkExtra || deathLinkStandard) && (parent.seedConfig?.DeathLinkMode ?? DeathLinkType.disabled) > DeathLinkType.disabled)
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.LastUsedConnectionInfo?.SlotName,
                    $"Failed to meet the requirements playing {manager.Song.Name} by {manager.Song.Artist}"));
        }

        public void RelayChatToYARG(LogMessage message)
        {
            if (message is ItemSendLogMessage ItemLog && ShouldRelayItemSend(ItemLog))
                ToastManager.ToastMessage(message.ToString());
            else if ((message is PlayerSpecificLogMessage || message is ServerChatLogMessage) && parent.seedConfig.InGameAPChat)
                ToastManager.ToastMessage(message.ToString());

            bool ShouldRelayItemSend(ItemSendLogMessage IL)
            {
                if (parent.seedConfig.InGameItemLog == CommonData.ItemLog.ToMe)
                    return IL.IsReceiverTheActivePlayer || IL.IsSenderTheActivePlayer;
                return parent.seedConfig.InGameItemLog == CommonData.ItemLog.All;
            }
        }

        public void VerifyServerConnection()
        {
            if (!parent.HasActiveSession)
                return;
            if (!parent.GetSession().Socket.Connected)
            {
                ToastManager.ToastWarning("Lost Connection to Archipelago");
                parent.Disconnect();
            }
        }

        public void UpdateAPData()
        {
            parent.UpdateReceivedItems();
            parent.UpdateCheckedLocations();
            if (parent.IsInSong(out _, out _) || !YargEngineActions.UpdateRecommendedSongsMenu())
                APPatches.HasAvailableAPSongUpdate = true;
            PendingTrapsFiller = true;
        }

        public bool PendingTrapsFiller = false;

        public void ApplyPendingTrapsFiller()
        {
            if (!PendingTrapsFiller) return;
            if (!parent.IsInSong(out _, out var buffer)) return;
            if (buffer == null || buffer < TimeSpan.FromSeconds(5)) return;

            var Pending = parent.ApItemsRecieved
                .Where(x => CommonData.IsActionable(x.Type) && !parent.seedConfig.ApItemsUsed.Contains(x)).ToList();

            if (Pending.Count == 0)
            {
                PendingTrapsFiller = false;
                return;
            }

            var Item = Pending[0];
            switch (Item.Type)
            {
                case StaticItems.StarPower:
                    YargEngineActions.ApplyStarPowerItem(parent);
                    break;
                case StaticItems.TrapRestart:
                    YargEngineActions.ForceRestartSong(parent);
                    break;
                case StaticItems.TrapRockMeter:
                    YargEngineActions.ApplyRockMetertrapItem(parent);
                    break;
            }
            parent.seedConfig.ApItemsUsed.Add(Item);
            parent.seedConfig.Save();
        }

        internal void OnDeathLinkReceived(DeathLink deathLink)
        {
            if (!parent.HasActiveSession) return;
            if (parent.seedConfig.DeathLinkMode <= DeathLinkType.disabled) return;
            YargEngineActions.ApplyDeathLink(parent, deathLink);
        }
    }

    public class SyncTimer
    {
        public SyncTimer()
        {
            timer.Elapsed += SyncTimerTick;
        }

        private Timer timer = new Timer(200);

        private bool ShouldUpdate = true; //Start true so we do an update when it initializes
        public event Action ConstantCallback;
        public event Action OnUpdateCallback;

        public void StartTimer()
        {
            SyncTimerTick(this, null);
            timer.Start();
        }
        public void StopTimer()
        {
            timer.Stop();
        }

        public void FlagUpdate() => ShouldUpdate = true;

        public void SyncTimerTick(object sender, ElapsedEventArgs e)
        {
            ConstantCallback?.Invoke();
            if (!ShouldUpdate) return;
            ShouldUpdate = false;
            OnUpdateCallback?.Invoke();
        }
    }
    public static partial class ExtraAPFunctionalityHelper
    {
        public const long minEnergyLinkScale = 20000;
        public const long maxEnergyLinkScale = 1000000;
        public static Dictionary<StaticItems, long> PriceDict = new Dictionary<StaticItems, long>
        {
            { StaticItems.SwapRandom, 17_000_000_000 },
            { StaticItems.SwapPick, 20_000_000_000 },
            { StaticItems.LowerDifficulty, 15_000_000_000 }
        };

    public static string EnergyLinkKey(ArchipelagoSession session) => $"EnergyLink{session.Players.ActivePlayer.Team}";
        public static bool TryPurchaseItem(APConnectionContainer container, StaticItems Type)
        {
            var CurrentEnergy = GetEnergy(container);
            if (!PriceDict.TryGetValue(Type, out var Price))
                return false;
            if (!TryUseEnergy(container, Price))
                return false;
            var CurCount = container.seedConfig.ApItemsPurchased.Where(x => x.Type == Type).Count();
            container.seedConfig.ApItemsPurchased.Add(new StaticYargAPItem(Type, StaticItemIDbyValue[Type], -99, CurCount, "YAYARG"));
            container.seedConfig.Save();
            return true;
        }

        public static string FormatLargeNumber(long number)
        {
            if (number >= 1_000_000_000_000)
                return (number / 1_000_000_000_000.0).ToString("0.##") + " Trillion";
            if (number >= 1_000_000_000)
                return (number / 1_000_000_000.0).ToString("0.##") + " Billion";
            if (number >= 1_000_000)
                return (number / 1_000_000.0).ToString("0.##") + " Million";
            if (number >= 1_000)
                return (number / 1_000.0).ToString("0.##") + " Thousand";

            return number.ToString("N0");
        }
        public static void SendScoreAsEnergy(APConnectionContainer container, long BaseScore, bool WasLocationChecked)
        {
            if (container.seedConfig.EnergyLinkMode <= CommonData.EnergyLinkType.disabled) return;
            if (container.seedConfig.EnergyLinkMode == CommonData.EnergyLinkType.check_song && !WasLocationChecked) return;
            if (container.seedConfig.EnergyLinkMode == CommonData.EnergyLinkType.other_song && WasLocationChecked) return;

            var Session = container.GetSession();
            Session.DataStorage[EnergyLinkKey(Session)].Initialize(0);
            Session.DataStorage[EnergyLinkKey(Session)] += ScaleEnergyValue(container, BaseScore);
        }

        public static long ScaleEnergyValue(APConnectionContainer container, long baseAmount)
        {
            int AmountOfLocationsTotal = container.GetSession().Locations.AllLocations.Count;
            int AmountOfLocationsChecked = container.GetSession().Locations.AllLocationsChecked.Count;
            double completionPercentage = AmountOfLocationsChecked / AmountOfLocationsTotal;
            double scale = minEnergyLinkScale + (completionPercentage * (maxEnergyLinkScale - minEnergyLinkScale));
            long Energy = (long)(baseAmount * scale);
            return Energy;
        }

        public static long GetEnergy(APConnectionContainer container)
        {
            if (container.seedConfig.EnergyLinkMode <= CommonData.EnergyLinkType.disabled) return 0;
            var Session = container.GetSession();
            Session.DataStorage[EnergyLinkKey(Session)].Initialize(0);
            return Session.DataStorage[EnergyLinkKey(Session)];
        }

        public static bool TryUseEnergy(APConnectionContainer container, long Amount)
        {
            if (container.seedConfig.EnergyLinkMode <= CommonData.EnergyLinkType.disabled) return false;
            var Session = container.GetSession();
            Session.DataStorage[EnergyLinkKey(Session)].Initialize(0);
            if (Session.DataStorage[EnergyLinkKey(Session)] >= Amount)
            {
                Session.DataStorage[EnergyLinkKey(Session)] -= Amount;
                return true;
            }
            return false;

        }
    }

}
