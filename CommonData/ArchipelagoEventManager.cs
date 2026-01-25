using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using YARG.Core;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Settings;
using YARG.Song;
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
            List<(SongEntry, string)> SongEntries = new List<(SongEntry, string)>();
            foreach (var i in parent.SlotData.InstrumentToAPData)
            {
                if (!parent.ReceivedInstruments.ContainsKey(i.Key)) continue;
                foreach (var song in i.Value)
                {
                    if (!parent.ReceivedSongUnlockItems.ContainsKey(song.Value.UnlockItemID)) continue;
                    if (!song.Value.HasAvailableLocations(parent)) continue;
                    var songObj = song.Value.GetYargSongEntry();
                    if (songObj != null)
                        SongEntries.Add((songObj, i.Key.GetDescription()));
                }
            }
            YargEngineActions.InsertAPListViewSongs(__instance, __result, SongEntries);
            var calc = AccessTools.Method(typeof(MusicLibraryMenu), "CalculateCategoryHeaderIndices");
            calc.Invoke(__instance, new object[] { __result });
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
            if ((parent.seedConfig?.DeathLinkMode ?? DeathLinkType.None) > DeathLinkType.None)
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.LastUsedConnectionInfo?.SlotName,
                    $"Failed Song {gameManager.Song.Name} by {gameManager.Song.Artist}"));
        }

        public void TryCheckSongLocations(GameManager gameManager)
        {
            if (!parent.IsSessionConnected)
                return;
            var LocationsPlayed = parent.SlotData.LocationIDtoAPData.Values.Where(x => x.WasActiveSongInGame(gameManager));
            var DoDeathlink = false;
            List<long> LocationsToComplete = new List<long>();
            foreach (var i in LocationsPlayed)
            {
                if (!i.IsSongUnlocked(parent))
                    continue;
                if (!i.HasAvailableLocations(parent))
                    continue;
                var pool = i.GetPool(parent.SlotData);

                var MetStandard = pool.MetStandard(gameManager, out var deathLinkStandard);
                if (!MetStandard && deathLinkStandard) DoDeathlink = true;
                if (MetStandard) LocationsToComplete.Add(i.MainLocationID);

                var MetExtra = true;
                if (i.ExtraLocationID >= 0)
                {
                    MetExtra = pool.MetExtra(gameManager, out var deathLinkExtra);
                    if (!MetExtra && deathLinkExtra) DoDeathlink = true;
                    if (MetExtra) LocationsToComplete.Add(i.ExtraLocationID);
                }

                if (i.CompletionLocationID >= 0)
                    if (MetStandard && MetExtra) LocationsToComplete.Add(i.ExtraLocationID);
            }

            if (LocationsToComplete.Count > 0)
                parent.GetSession().Locations.CompleteLocationChecks(LocationsToComplete.ToArray());

            if (DoDeathlink && (parent.seedConfig?.DeathLinkMode ?? DeathLinkType.None) > DeathLinkType.None)
                parent.DeathLinkService?.SendDeathLink(
                    new DeathLink(parent.LastUsedConnectionInfo?.SlotName,
                    $"Failed to meet the requirements playing {gameManager.Song.Name} by {gameManager.Song.Artist}"));
        }

        public void RelayChatToYARG(LogMessage message)
        {
            if (message is ItemSendLogMessage ItemLog && ShouldRelay(ItemLog))
                ToastManager.ToastMessage(message.ToString());
            else if (message is ChatLogMessage && parent.seedConfig.InGameAPChat)
                ToastManager.ToastMessage(message.ToString());

            bool ShouldRelay(ItemSendLogMessage IL)
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
            if (parent.IsInSong() || !YargEngineActions.UpdateRecommendedSongsMenu())
                APPatches.HasAvailableAPSongUpdate = true;
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
}
