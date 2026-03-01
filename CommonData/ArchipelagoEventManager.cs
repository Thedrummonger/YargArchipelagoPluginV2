using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using YARG.Core;
using YARG.Core.Engine;
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
        public void InsertAPSongs(MusicLibraryMenu __instance, List<ViewType> __result) =>
            YargEngineActions.InsertAPListViewSongs(parent, __instance, __result);

        public void SetSong(GameManager gameManager) => parent.SetCurrentSong(gameManager);
        public void SetSong() => parent.ClearCurrentSong();

        public void FailedSong(GameManager gameManager)
        {
            if (!parent.IsSessionConnected || parent.seedConfig is null || !parent.seedConfig.SendDlOnSongFail())
                return;

            parent.DeathLinkService?.SendDeathLink(new DeathLink(parent.GetSession().Players.ActivePlayer.Name, $"Failed Song {gameManager.Song.Name} by {gameManager.Song.Artist}"));
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

                var MetStandard = gameManager.Players.MetStandardCheckRequirement(i, parent, out var deathLinkStandard) || ShouldCheat;
                if (!MetStandard && deathLinkStandard) DoDeathlink = true;
                if (MetStandard) LocationsToComplete.Add(i.MainLocationID);

                var MetExtra = true;
                if (i.ExtraLocationID >= 0)
                {
                    MetExtra = gameManager.Players.MetExtraCheckRequirement(i, parent, out var deathLinkExtra) || ShouldCheat;
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

            if (DoDeathlink && (parent.seedConfig?.SendDlOnRequirements() ?? false))
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.GetSession().Players.ActivePlayer.Name,
                    $"Failed to meet the requirements playing {gameManager.Song.Name} by {gameManager.Song.Artist}"));
        }
        internal void TryCheckSongGoalSong(GameManager manager)
        {
            if (!parent.IsSessionConnected || !parent.SlotData.GoalData.WasActiveSongInGame(parent, manager) || !parent.SlotData.GoalData.IsSongUnlocked(parent))
                return;

            var MetRequirements = manager.Players.MetAllCheckRequirments(parent.SlotData.GoalData, parent, out bool DL);

            if (MetRequirements)
                parent.GetSession().Locations.CompleteLocationChecks(parent.SlotData.GoalData.MainLocationID);

            if (DL && (parent.seedConfig?.SendDlOnRequirements()??false))
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.GetSession().Players.ActivePlayer.Name,
                    $"Failed to meet the requirements playing {manager.Song.Name} by {manager.Song.Artist}"));
        }

        public void RelayChatToYARG(LogMessage message)
        {
            bool Relay = false;
            if (parent.seedConfig.InGameItemLog == CommonData.ItemLog.All && parent.seedConfig.InGameAPChat) Relay = true;
            else if (message is ItemSendLogMessage ItemLog && ShouldRelayItemSend(ItemLog)) Relay = true;
            else if ((message is PlayerSpecificLogMessage || message is ServerChatLogMessage) && parent.seedConfig.InGameAPChat) Relay = true;

            var Flag = GetMessageToastFlag(message, out var wasProgressive);
            if (Relay && wasProgressive)
                ToastManager.ToastSuccess(message.ToYargColoredString().FlagAPToast(Flag));
            else if (Relay) 
                ToastManager.ToastMessage(message.ToYargColoredString().FlagAPToast(Flag));

            bool ShouldRelayItemSend(ItemSendLogMessage IL)
            {
                if (parent.seedConfig.InGameItemLog == CommonData.ItemLog.ToMe)
                    return IL.IsReceiverTheActivePlayer || IL.IsSenderTheActivePlayer;
                return parent.seedConfig.InGameItemLog == CommonData.ItemLog.All;
            }
        }

        private YargAPUtils.APToastFlags GetMessageToastFlag(LogMessage message, out bool WasProgressive)
        {
            WasProgressive = false;
            if (message is ItemSendLogMessage itemSendLog)
            {
                WasProgressive = itemSendLog.Item.Flags.HasFlag(Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement);
                if (WasProgressive || itemSendLog.Item.Flags.HasFlag(Archipelago.MultiClient.Net.Enums.ItemFlags.NeverExclude))
                    return YargAPUtils.APToastFlags.GoodItem;
                return YargAPUtils.APToastFlags.JunkItem;
            }
            return YargAPUtils.APToastFlags.Message;
        }

        public void UpdateChatHistory(LogMessage message) => ArchipelagoConnectionDialog.ChatHistory.Add(message);

        public void VerifyServerConnection()
        {
            if (!parent.HasActiveSession)
                return;
            if (!parent.GetSession().Socket.Connected)
            {
                ToastManager.ToastWarning($"Lost Connection to Archipelago".FlagAPToast());
                parent.Disconnect();
            }
        }

        public void UpdateAPData()
        {
            parent.UpdateReceivedItems();
            parent.UpdateCheckedLocations();
            FlagSongLibraryForUpdate();
            PendingTrapsFiller = true;
        }

        public void FlagSongLibraryForUpdate()
        {
            if (parent.IsInSong(out _, out _) || !YargEngineActions.UpdateRecommendedSongsMenu())
                APPatches.HasAvailableAPSongUpdate = true;
        }

        public bool PendingTrapsFiller = false;
        private static readonly TimeSpan fillerBuffer = TimeSpan.FromSeconds(5);

        public void ApplyPendingTrapsFiller(GameManager gm)
        {
            if (!PendingTrapsFiller || !parent.IsSessionConnected) 
                return;

            if (!parent.IsInSong(out var Song, out var buffer) || !Song.CouldProductLocationCheck(parent, out _) || buffer < fillerBuffer)
                return;

            StaticYargAPItem[] Pending;
            try 
            {
                var UsedItems = parent.seedConfig.ApItemsUsed.ToHashSet();
                Pending = parent.ApItemsRecieved.ToArray();
                Pending = Pending.Where(x => x.Type.IsActionable() && !UsedItems.Contains(x)).ToArray();
            }
            catch (Exception ex)
            {
                parent.logger.LogWarning($" Failed to snapshot received items {ex}");
                return;
            }

            if (Pending.Length == 0)
            {
                PendingTrapsFiller = false;
                return;
            }
            var Item = Pending[0];

            parent.seedConfig.ApItemsUsed.Add(Item);
            parent.seedConfig.Save();

            var FromPlayer = Item.GetPlayerInfo(parent);
            switch (Item.Type)
            {
                case StaticItems.StarPower:
                    ToastManager.ToastInformation($"{FromPlayer.Name} sent you Star Power!".FlagAPToast());
                    YargEngineActions.ApplyStarPowerItem(parent);
                    break;
                case StaticItems.TrapRestart:
                    parent.ResetBuffer();
                    ToastManager.ToastWarning($"{FromPlayer.Name} sent you a Restart Trap!".FlagAPToast());
                    YargEngineActions.ForceRestartSong(parent);
                    break;
                case StaticItems.TrapRockMeter:
                    ToastManager.ToastWarning($"{FromPlayer.Name} sent you a Rock Meter Trap!".FlagAPToast());
                    YargEngineActions.ApplyRockMetertrapItem(parent);
                    break;
            }
        }

        internal void OnDeathLinkReceived(DeathLink deathLink)
        {
            if (!parent.HasActiveSession) return;
            if (parent.seedConfig.DeathLinkMode <= DeathLinkType.disabled) return;
            YargEngineActions.ApplyDeathLink(parent, deathLink);
        }

        private bool IsPreventingSongFail = false;
        internal void TryUseSongFailPrevent(EngineManager engineManager)
        {
            if (IsPreventingSongFail || !parent.IsSessionConnected || !parent.IsInSong(out var gameManager, out _)) return;

            if (engineManager.Happiness < 0.0f && !gameManager.PlayerHasFailed && gameManager.CouldProductLocationCheck(parent, out _))
            {
                var Pending = parent.ApItemsRecieved
                    .Where(x => x.Type == StaticItems.FailPrevention && !parent.seedConfig.ApItemsUsed.Contains(x)).ToList();
                if (Pending.Count <= 0)
                    return;
                IsPreventingSongFail = true;
                YargEngineActions.PreventSongFail(engineManager);
                var ToUse = Pending.First();
                var Player = ToUse.GetPlayerInfo(parent);
                ToastManager.ToastSuccess($"{Player.Name} cheered you on!".FlagAPToast());
                parent.seedConfig.ApItemsUsed.Add(ToUse);
                parent.seedConfig.Save();
                IsPreventingSongFail = false;
            }
            
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
            { StaticItems.SwapRandom, 30_000_000_000 },
            { StaticItems.SwapPick, 35_000_000_000 },
            { StaticItems.LowerDifficulty, 32_000_000_000 }
        };

        public static string EnergyLinkKey(ArchipelagoSession session) => $"EnergyLink{session.Players.ActivePlayer.Team}";
        public static bool TryPurchaseItem(APConnectionContainer container, StaticItems Type)
        {
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
