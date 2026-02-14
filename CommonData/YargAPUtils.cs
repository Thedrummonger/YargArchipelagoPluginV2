using Archipelago.MultiClient.Net.MessageLog.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay;
using YARG.Menu.Persistent;
using YARG.Scores;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;
using static YargArchipelagoPlugin.YargAPUtils;

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

        public enum APToastFlags
        {
            [Description("[AP]"), APAssets.APIcon(APAssets.APIcon.White)]
            Message,
            [Description("[APP]"), APAssets.APIcon(APAssets.APIcon.Color)]
            GoodItem,
            [Description("[APU]"), APAssets.APIcon(APAssets.APIcon.Blue)]
            JunkItem,
        }
        public static readonly string APToastFlag = APToastFlags.Message.GetDescription();
        private static readonly Dictionary<string, APToastFlags> ToastPrefixes = Enum.GetValues(typeof(APToastFlags))
            .Cast<APToastFlags>().ToDictionary(x => x.GetDescription(),x => x,StringComparer.OrdinalIgnoreCase);

        public static void TestFlags()
        {
            var Flags = Enum.GetValues(typeof(APToastFlags)).Cast<APToastFlags>();
            foreach(var Flag in Flags)
            {
                ToastManager.ToastMessage(Flag.GetDescription() + "General");
                ToastManager.ToastInformation(Flag.GetDescription() + "Information");
                ToastManager.ToastSuccess(Flag.GetDescription() + "Success");
                ToastManager.ToastWarning(Flag.GetDescription() + "Warning");
                ToastManager.ToastError(Flag.GetDescription() + "Error");
            }
        }

        public static bool HandleAPToasts(ToastManager manager, object type, string body, Action onClick)
        {
            var match = ToastPrefixes.FirstOrDefault(x => body.StartsWith(x.Key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null) return false;
            var flag = match.Value;

            try
            {
                body = body.Substring(flag.GetDescription().Length).TrimStart();
                int typeValue = Convert.ToInt32(type);
                var (color, icon) = YargAPUtils.ResolveToastVisuals(manager, typeValue);
                var prefab = (Toast)AccessTools.Field(typeof(ToastManager), "_toastPrefab").GetValue(manager);
                UnityEngine.Object.Instantiate(prefab, manager.transform).Initialize("Archipelago", body, APAssets.Get(flag.GetIcon()), color, onClick);
                return true;
            }
            catch (Exception ex) { Debug.LogError($"Failed to create custom toast\n{ex}"); }
            return false;
        }

        public static bool CouldProductLocationCheck(this GameManager song, APConnectionContainer container, out IEnumerable<SongAPData> APSongEntries)
        {
            APSongEntries = container.SlotData.Songs.Where(x => 
                x.WasActiveSongInGame(container, song) && 
                x.HasAvailableLocations(container) && 
                x.IsSongUnlocked(container));
            return APSongEntries.Any();
        }
    }

    public static class APAssets
    {
        public enum APIcon
        {
            Black,
            Blue,
            Color,
            White
        }

        [AttributeUsage(AttributeTargets.Field)]
        public sealed class APIconAttribute : Attribute
        {
            public APIcon Icon { get; }
            public APIconAttribute(APIcon icon) { Icon = icon; }
        }
        public static APIcon GetIcon(this APToastFlags value)
        {
            return value.GetType()?.GetField(value.ToString())?.GetCustomAttributes(typeof(APIconAttribute), false)?
                .Cast<APIconAttribute>()?.FirstOrDefault()?.Icon?? APIcon.White;
        }

        static Sprite _black, _blue, _color, _white;

        public static Sprite Get(APIcon icon)
        {
            switch (icon)
            {
                case APIcon.Black: return _black ?? (_black = Load("black-icon.png"));
                case APIcon.Blue: return _blue ?? (_blue = Load("blue-icon.png"));
                case APIcon.Color: return _color ?? (_color = Load("color-icon.png"));
                case APIcon.White: return _white ?? (_white = Load("white-icon.png"));
                default: return null;
            }
        }
        static MethodInfo _loadImage;
        static Sprite Load(string suffix)
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                          .First(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            byte[] data;

            using (var s = asm.GetManifestResourceStream(name))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                data = ms.ToArray();
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            (_loadImage ?? (_loadImage = GetLoadImageMI())).Invoke(null, new object[] { tex, data, false });

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }

        static MethodInfo GetLoadImageMI()
        {
            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                 ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine");

            return t.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
        }
    }
}
