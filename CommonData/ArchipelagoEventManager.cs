using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
#if !CLONE_HERO
using YARG.Gameplay;
#endif
using YargArchipelagoCommon;
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public class ArchipelagoEventManager
    {
        public static void ApplyDeathLink(APConnectionContainer container, DeathLink deathLink)
        {
            try
            {
                container.LogInfo?.Invoke("Applying Death Link");
                if (!EngineActions.ApplyDeathLink(container)) return;
                APToastManager.ToastInformation($"DeathLink Received!\n\n{deathLink?.Source ?? "Debug"} {deathLink?.Cause ?? "Command"}");
            }
            catch (Exception e)
            {
                container.LogError?.Invoke($"Failed to apply deathlink\n{e}");
            }
        }


        public ArchipelagoEventManager (APConnectionContainer connectionContainer) { parent = connectionContainer; }
        APConnectionContainer parent;

        public void SetSong(GameManager gameManager) => parent.SetCurrentSong(gameManager);
        public void SetSong() => parent.ClearCurrentSong();

        public void TryCheckSongLocations(APSongResult gameManager)
        {
            bool ShouldCheat = APPatches.IgnoreScoreForNextSong;
            APPatches.IgnoreScoreForNextSong = false;
            if (!parent.IsSessionConnected)
                return;

            if (gameManager.IsPractice || gameManager.PlayerHasFailed)
                return;

            var Players = gameManager.Players;
            var LocationsPlayed = parent.SlotData.Songs.Where(x => parent.SongHashLookup.Comparer.Equals(x.GetActiveHash(parent), gameManager.Hash));
            var DoDeathlink = false;
            List<long> LocationsToComplete = new List<long>();
            foreach (var i in LocationsPlayed)
            {
                if (!i.IsSongUnlocked(parent))
                    continue;
                if (!i.HasAvailableLocations(parent))
                    continue;

                var MetStandard = Players.MetStandardCheckRequirement(i, parent, out var deathLinkStandard) || ShouldCheat;
                if (!MetStandard && deathLinkStandard) DoDeathlink = true;
                if (MetStandard) LocationsToComplete.Add(i.MainLocationID);

                var MetExtra = true;
                if (i.ExtraLocationID >= 0)
                {
                    MetExtra = Players.MetExtraCheckRequirement(i, parent, out var deathLinkExtra) || ShouldCheat;
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
                    $"Failed to meet the requirements playing {gameManager.Name}"));
        }
        internal void TryCheckSongGoalSong(APSongResult manager)
        {
            if (!parent.IsSessionConnected || !parent.SongHashLookup.Comparer.Equals(parent.SlotData.GoalData.GetActiveHash(parent), manager.Hash) || !parent.SlotData.GoalData.IsSongUnlocked(parent))
                return;

            if (manager.IsPractice || manager.PlayerHasFailed)
                return;


            var MetRequirements = manager.Players.MetAllCheckRequirments(parent.SlotData.GoalData, parent, out bool DL);

            if (MetRequirements)
                parent.GetSession().Locations.CompleteLocationChecks(parent.SlotData.GoalData.MainLocationID);

            if (DL && (parent.seedConfig?.SendDlOnRequirements()??false))
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.GetSession().Players.ActivePlayer.Name,
                    $"Failed to meet the requirements playing {manager.Name}"));
        }

        public void RelayChat(LogMessage message)
        {
            bool Relay = false;
            if (parent.seedConfig.InGameItemLog == ItemLog.All && parent.seedConfig.InGameAPChat) Relay = true;
            else if (message is ItemSendLogMessage ItemLog && ShouldRelayItemSend(ItemLog)) Relay = true;
            else if ((message is PlayerSpecificLogMessage || message is ServerChatLogMessage) && parent.seedConfig.InGameAPChat) Relay = true;

            if (Relay) APToastManager.AddToast(GetMessageToastFlag(message), message.ToColoredString());

            bool ShouldRelayItemSend(ItemSendLogMessage IL)
            {
                if (parent.seedConfig.InGameItemLog == ItemLog.ToMe)
                    return IL.IsReceiverTheActivePlayer || IL.IsSenderTheActivePlayer;
                return parent.seedConfig.InGameItemLog == ItemLog.All;
            }
        }

        private APToastManager.APToastType GetMessageToastFlag(LogMessage message)
        {
            if (message is ItemSendLogMessage itemSendLog)
            {
                if (itemSendLog.Item.Flags.HasFlag(Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement))
                    return APToastManager.APToastType.Progression;
                if (itemSendLog.Item.Flags.HasFlag(Archipelago.MultiClient.Net.Enums.ItemFlags.NeverExclude))
                    return APToastManager.APToastType.Useful;
                if (itemSendLog.Item.Flags.HasFlag(Archipelago.MultiClient.Net.Enums.ItemFlags.Trap))
                    return APToastManager.APToastType.Trap;
                return APToastManager.APToastType.Junk;
            }
            return APToastManager.APToastType.General;
        }

        public static List<LogMessage> ChatHistory = new List<LogMessage>();

        public void UpdateChatHistory(LogMessage message) => ChatHistory.Add(message);

        public static void FlagSongLibraryForUpdate() => APPatches.HasAvailableAPSongUpdate = true;

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
                parent.LogWarning?.Invoke($" Failed to snapshot received items {ex}");
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
                    APToastManager.ToastInformation($"{FromPlayer.Name} sent you Star Power!");
                    EngineActions.ApplyStarPowerItem(parent);
                    break;
                case StaticItems.TrapRestart:
                    APToastManager.ToastWarning($"{FromPlayer.Name} sent you a Restart Trap!");
                    EngineActions.ForceRestartSong(parent);
                    break;
                case StaticItems.TrapRockMeter:
                    APToastManager.ToastWarning($"{FromPlayer.Name} sent you a Rock Meter Trap!");
                    EngineActions.ApplyRockMetertrapItem(parent);
                    break;
            }
        }

        internal void OnDeathLinkReceived(DeathLink deathLink)
        {
            if (!parent.IsSessionConnected) return;
            if (parent.seedConfig.DeathLinkMode <= DeathLinkType.disabled) return;
            ApplyDeathLink(parent, deathLink);
        }

    }
}
