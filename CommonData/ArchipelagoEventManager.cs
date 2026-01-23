using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Scores;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData.Networking;

namespace YargArchipelagoPlugin
{
    public class ArchipelagoEventManager
    {
        public ArchipelagoService APHandler { get; private set; }
        public ArchipelagoEventManager(ArchipelagoService Handler)
        {
            APHandler = Handler;
        }
        public void SendSongCompletionResults(CommonData.SongCompletedData songCompletedData)
        {
            _ = APHandler.packetClient?.SendPacketAsync(new YargAPPacket { SongCompletedInfo = songCompletedData });
        }

        public void SongStarted(GameManager gameManager)
        {
            APHandler.SetCurrentSong(gameManager);
            _ = APHandler.packetClient?.SendPacketAsync(new YargAPPacket
            {
                CurrentlyPlaying = CommonData.CurrentlyPlayingData.CurrentlyPlayingSong(gameManager.Song.ToSongData())
            });
        }

        public void SongEnded()
        {
            APHandler.ClearCurrentSong();
            APHandler.HasAvailableSongUpdate = true;
            _ = APHandler.packetClient?.SendPacketAsync(new YargAPPacket
            {
                CurrentlyPlaying = CommonData.CurrentlyPlayingData.CurrentlyPlayingNone()
            });
        }
    }
}
