using Archipelago.MultiClient.Net.MessageLog.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay;
using YARG.Menu.Persistent;
using YARG.Scores;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace YargArchipelagoPlugin
{
    public static class YargAPUtils
    {
        private static string[] Colors = new[]
        {
            "#C97682",
            "#75C275",
            "#CA94C2",
            "#D9A07D",
            "#767EBD",
            "#EEE391"
        };
        public static T CycleEnum<T>(T currentValue) where T : System.Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int currentIndex = Array.IndexOf(values, currentValue);
            int nextIndex = (currentIndex + 1) % values.Length;
            return values[nextIndex];
        }
        public static string ToRainbowString(this string input)
        {
            var result = new StringBuilder();
            int colorIndex = 0;
            foreach (char c in input)
            {
                if (char.IsWhiteSpace(c)) 
                    result.Append(c);
                else
                {
                    result.Append($"<color={Colors[colorIndex]}>{c}</color>");
                    colorIndex = colorIndex + 1 >= Colors.Length ? 0 : colorIndex + 1;
                }
            }
            return result.ToString();
        }
        public static string ToYargColoredString(this LogMessage message)
        {
            var result = new StringBuilder();
            foreach (var i in message.Parts)
            {
                var hexColor = $"#{i.Color.R:X2}{i.Color.G:X2}{i.Color.B:X2}";
                result.Append($"<color={hexColor}>{i.Text}</color>");
            }
            return result.ToString();
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

        public static (UnityEngine.Color color, UnityEngine.Sprite icon) ResolveToastVisuals(ToastManager manager, int typeValue)
        {
            // For some reason YARG has to make everything private so we have to hack out the type value of the original toast.
            string typeName;
            switch (typeValue)
            {
                case 0: typeName = "General"; break;
                case 1: typeName = "Information"; break;
                case 2: typeName = "Success"; break;
                case 3: typeName = "Warning"; break;
                case 4: typeName = "Error"; break;
                default: typeName = "General"; break;
            }
            var colorField = AccessTools.Field(typeof(ToastManager), "_" + typeName.ToLower() + "Color");
            var iconField = AccessTools.Field(typeof(ToastManager), "_icon" + typeName);
            var color = (UnityEngine.Color)colorField.GetValue(manager);
            var icon = (UnityEngine.Sprite)iconField.GetValue(manager);

            return (color, icon);
        }
    }
}
