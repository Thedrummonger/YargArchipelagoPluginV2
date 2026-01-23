using System;
using System.Collections.Generic;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Core;
using YargArchipelagoCommon;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Scores;

namespace YargArchipelagoPlugin
{
    public static class YargAPUtils
    {
        public static bool IsSupportedInstrument(Instrument source, out CommonData.SupportedInstrument? target)
        {
            int value = (int)source;
            if (Enum.IsDefined(typeof(CommonData.SupportedInstrument), value))
            {
                target = (CommonData.SupportedInstrument)value;
                return true;
            }
            target = default;
            return false;
        }
        public static CommonData.SupportedDifficulty GetSupportedDifficulty(Difficulty source)
        {
            if (source > Difficulty.Expert)
                return CommonData.SupportedDifficulty.Expert;
            if (source < Difficulty.Easy)
                return CommonData.SupportedDifficulty.Easy;
            return (CommonData.SupportedDifficulty)(int)source;
        }
        public static CommonData.SongData ToSongData(this SongEntry song)
        {
            return new CommonData.SongData()
            {
                Name = RichTextUtils.StripRichTextTags(song.Name),
                Artist = RichTextUtils.StripRichTextTags(song.Artist),
                SongChecksum = Convert.ToBase64String(song.Hash.HashBytes),
                Difficulties = new Dictionary<CommonData.SupportedInstrument, int>()
            };
        }

        public static CommonData.SongParticipantInfo[] CreatePlayerScores(GameManager __instance)
        {
            List<CommonData.SongParticipantInfo> participants = new List<CommonData.SongParticipantInfo>();
            foreach (var player in __instance.Players)
            {
                var Profile = player.Player.Profile;
                if (!IsSupportedInstrument(Profile.CurrentInstrument, out var SupportedInstrument)) continue;
                if (!ScoreContainer.IsSoloScoreValid(__instance.SongSpeed, player.Player)) continue;
                var Participant = new CommonData.SongParticipantInfo()
                {
                    Difficulty = GetSupportedDifficulty(Profile.CurrentDifficulty),
                    instrument = SupportedInstrument,
                    FC = player.IsFc,
                    Percentage = player.BaseStats.Percent,
                    Score = player.Score,
                    Stars = (int)player.Stars,
                    WasGoldStar = StarAmountHelper.GetStarsFromInt((int)player.Stars) == StarAmount.StarGold,
                };
                participants.Add(Participant);
            }
            return participants.ToArray();
        }
        public static (string Ip, int Port) ParseIpAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (null, 0);
            var parts = input.Split(':');
            string ip = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 38281;
            return (ip, port);
        }

    }
}
