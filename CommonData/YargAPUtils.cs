using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay;
using YARG.Scores;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public static class YargAPUtils
    {
        public static T CycleEnum<T>(T currentValue) where T : System.Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int currentIndex = Array.IndexOf(values, currentValue);
            int nextIndex = (currentIndex + 1) % values.Length;
            return values[nextIndex];
        }
        public static bool IsSupportedInstrument(Instrument source, out CommonData.SupportedInstrument? target)
        {
            if (Enum.TryParse<CommonData.SupportedInstrument>(source.ToString(), out var result))
            {
                target = result;
                return true;
            }
            target = null;
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
        public static (string Ip, int Port) ParseIpAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (null, 0);
            var parts = input.Split(':');
            string ip = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 38281;
            return (ip, port);
        }
        public static string GetDescription(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttributes(typeof(DescriptionAttribute), false)
             .OfType<DescriptionAttribute>()
             .FirstOrDefault()?.Description ?? value.ToString();

        public static bool MetStandard(this SongPool pool, GameManager passInfo, out bool DeathLink, CompletionRequirements CustomReqs = null) =>
            pool.MetReq(passInfo, out DeathLink, (CustomReqs ?? pool.completion_requirements).reward1_req, (CustomReqs ?? pool.completion_requirements).reward1_diff);
        public static bool MetExtra(this SongPool pool, GameManager passInfo, out bool DeathLink, CompletionRequirements CustomReqs = null) =>
            pool.MetReq(passInfo, out DeathLink, (CustomReqs ?? pool.completion_requirements).reward2_req, (CustomReqs ?? pool.completion_requirements).reward2_diff);

        private static bool MetReq(this SongPool pool, GameManager passInfo, out bool DeathLink, CompletionReq req, SupportedDifficulty diff)
        {
            // Only send a deathlink if we had a player playing the correct instrument
            // at the correct difficulty and they failed to meet the score requirement.
            var HadValidPlayer = false;
            foreach (var player in passInfo.Players)
            {
                if (!IsSupportedInstrument(player.Player.Profile.CurrentInstrument, out SupportedInstrument? inst)) continue;
                if (inst != pool.instrument) continue;
                if (GetSupportedDifficulty(player.Player.Profile.CurrentDifficulty) < diff) continue;
                HadValidPlayer = true;
                if (req == CompletionReq.FullCombo && !player.IsFc) continue;
                bool WasGold = StarAmountHelper.GetStarsFromInt((int)player.Stars) == StarAmount.StarGold;
                if (req == CompletionReq.GoldStar && !WasGold) continue;
                if (player.Stars < (int)req) continue;
                DeathLink = false;
                return true;
            }
            DeathLink = HadValidPlayer;
            return false;
        }
    }
}
