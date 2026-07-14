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
using YARG.Gameplay.Player;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Scores;
using YARG.Song;
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
        public static string ToYargColoredString(this string message, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>";

        public static string ToYargColoredString(this bool val) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(val ? Color.green : Color.red)}>{val}</color>";
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

        static readonly HashSet<Instrument> YargBaseDrums = [Instrument.FourLaneDrums, Instrument.FiveLaneDrums];
        static readonly HashSet<SupportedInstrument> APBaseDrums = [SupportedInstrument.FourLaneDrums, SupportedInstrument.FiveLaneDrums];

        static readonly HashSet<Instrument> YargProDrums = [Instrument.ProDrums, Instrument.EliteDrums];
        static readonly HashSet<SupportedInstrument> APAllDrums = [SupportedInstrument.ProDrums, .. APBaseDrums];

        public static bool PlayedValidInsturmentForCheck(Instrument YargInstrument, SupportedInstrument CheckInstrument)
        {
            HashSet<SupportedInstrument> PlayedInstruments = [];

            // Make four lane and five lane charts interchangable because thats how yarg treats them.
            if (YargBaseDrums.Contains(YargInstrument))
                foreach(var i in APBaseDrums)
                    PlayedInstruments.Add(i);   

            // Pro and Elite drums can clear any Drum chart
            if (YargProDrums.Contains(YargInstrument))
                foreach (var i in APAllDrums)
                    PlayedInstruments.Add(i);

            if (IsSupportedInstrument(YargInstrument, out var Played))
                PlayedInstruments.Add(Played.Value);   

            return PlayedInstruments.Contains(CheckInstrument);
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

        public static bool MetStandardCheckRequirement(this IEnumerable<BasePlayer> Players, BaseAPSong song, APConnectionContainer container, out bool DeathLink) =>
            MetSongCheckRequirements(Players, song, container, false, out DeathLink);
        public static bool MetExtraCheckRequirement(this IEnumerable<BasePlayer> Players, BaseAPSong song, APConnectionContainer container, out bool DeathLink) =>
            MetSongCheckRequirements(Players, song, container, true, out DeathLink);
        public static bool MetAllCheckRequirments(this IEnumerable<BasePlayer> Players, BaseAPSong song, APConnectionContainer container, out bool DeathLink)
        {
            var MetStandard = MetStandardCheckRequirement(Players, song, container, out bool DLS);
            var MetExtra = MetExtraCheckRequirement(Players, song, container, out bool DLE);
            DeathLink = DLS || DLE;
            return MetStandard && MetExtra;
        }
        public static bool MetSongCheckRequirements(this IEnumerable<BasePlayer> Players, BaseAPSong song, APConnectionContainer container, bool ExtraCheck, out bool DeathLink)
        {
            var CurrentRequirements = song.GetCurrentCompletionRequirements(container);
            var diff = ExtraCheck ? CurrentRequirements.reward2_diff : CurrentRequirements.reward1_diff;
            var req = ExtraCheck ? CurrentRequirements.reward2_req : CurrentRequirements.reward1_req;
            var pool = song.GetPool(container.SlotData);
            // Only send a deathlink if we had a player playing the correct instrument
            // at the correct difficulty and they failed to meet the score requirement.
            var HadValidPlayer = false;
            foreach (var player in Players)
            {
                if (!PlayedValidInsturmentForCheck(player.Player.Profile.CurrentInstrument, pool.instrument)) continue;
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

        public static bool CouldProductLocationCheck(this GameManager song, APConnectionContainer container, out IEnumerable<SongAPData> APSongEntries)
        {
            APSongEntries = container.SlotData.Songs.Where(x => 
                x.WasActiveSongInGame(container, song) && 
                x.HasAvailableLocations(container) && 
                x.IsSongUnlocked(container));
            return APSongEntries.Any();
        }

        public static string GetSongNameFromLocationString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int index = input.LastIndexOf("on ", StringComparison.Ordinal);
            return index >= 0 ? input.Substring(0, index) : input;
        }

        public static string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength) + "...";
        }
    }

    public static class APToastManager
    {
        public enum APToastType
        {
            General = 100,
            Information = 101,
            Success = 102,
            Warning = 103,
            Error = 104,
            Junk = 105,
            Useful = 106,
            Progression = 107,
            Trap = 108
        }
        private static readonly AccessTools.FieldRef<ToastManager, Toast> ToastPrefabRef =
            AccessTools.FieldRefAccess<ToastManager, Toast>("_toastPrefab");

        private static readonly AccessTools.FieldRef<ToastManager, Color> GeneralColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_generalColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> InformationColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_informationColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> SuccessColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_successColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> WarningColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_warningColor");

        private static readonly AccessTools.FieldRef<ToastManager, Color> ErrorColorRef =
            AccessTools.FieldRefAccess<ToastManager, Color>("_errorColor");

        private static readonly Type ToastManagerType = typeof(ToastManager);

        private static readonly Type ToastTypeEnum = ToastManagerType.GetNestedType("ToastType", BindingFlags.NonPublic);

        private static readonly MethodInfo AddToastMethod =
            AccessTools.Method(ToastManagerType, "AddToast", [ToastTypeEnum, typeof(string), typeof(Action)] );

        public static void AddToast(APToastType type, string text, Action onClick = null)
        {
            object enumValue = Enum.ToObject(ToastTypeEnum, (int)type);
            AddToastMethod.Invoke(null, [enumValue, text, onClick]);
        }
        public static void ToastMessage(string text, Action onClick = null) => AddToast(APToastType.General, text, onClick);
        public static void ToastInformation(string text, Action onClick = null) => AddToast(APToastType.Information, text, onClick);
        public static void ToastSuccess(string text, Action onClick = null) => AddToast(APToastType.Success, text, onClick);
        public static void ToastWarning(string text, Action onClick = null) => AddToast(APToastType.Warning, text, onClick);
        public static void ToastError(string text, Action onClick = null) => AddToast(APToastType.Error, text, onClick);
        public static void ToastJunkItem(string text, Action onClick = null) => AddToast(APToastType.Junk, text, onClick);
        public static void ToastUsefulItem(string text, Action onClick = null) => AddToast(APToastType.Useful, text, onClick);
        public static void ToastProgressionItem(string text, Action onClick = null) => AddToast(APToastType.Progression, text, onClick);
        public static void ToastTrapItem(string text, Action onClick = null) => AddToast(APToastType.Trap, text, onClick);

        public static bool HandleAPToasts(int type, string body, Action onClick, ToastManager manager)
        {
            if (type < 100) return false;
            var ToastType = (APToastType)type;

            var (text, color, icon) = ToastType switch
            {
                APToastType.General => ("Archipelago", GeneralColorRef(manager), APAssets.Get(APAssets.APIcon.White)),
                APToastType.Information => ("Archipelago", InformationColorRef(manager), APAssets.Get(APAssets.APIcon.White)),
                APToastType.Success => ("Archipelago", SuccessColorRef(manager), APAssets.Get(APAssets.APIcon.White)),
                APToastType.Warning => ("Archipelago", WarningColorRef(manager), APAssets.Get(APAssets.APIcon.White)),
                APToastType.Error => ("Archipelago", ErrorColorRef(manager), APAssets.Get(APAssets.APIcon.White)),
                APToastType.Junk => ("Archipelago", Color.cyan, APAssets.Get(APAssets.APIcon.Blue)),
                APToastType.Useful => ("Archipelago", Color.slateBlue, APAssets.Get(APAssets.APIcon.Color)),
                APToastType.Progression => ("Archipelago", Color.plum, APAssets.Get(APAssets.APIcon.Color)),
                APToastType.Trap => ("Archipelago", Color.salmon, APAssets.Get(APAssets.APIcon.Blue)),
                _ => throw new ArgumentException($"Invalid toast type {type}!")
            };

            var toast = UnityEngine.Object.Instantiate(ToastPrefabRef(manager), manager.transform);
            toast.Initialize(text, body, icon, color, onClick);
            return true;
        }

        public static void TestToasts()
        {
            ToastMessage("Message");
            ToastJunkItem("Junk");
            ToastUsefulItem("Useful");
            ToastProgressionItem("Progression");
            ToastTrapItem("Trap");
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

        static Sprite _black, _blue, _color, _white;

        public static Sprite Get(APIcon icon) => icon switch
        {
            APIcon.Black => _black ??= Load("black-icon.png"),
            APIcon.Blue => _blue ??= Load("blue-icon.png"),
            APIcon.Color => _color ??= Load("color-icon.png"),
            APIcon.White => _white ??= Load("white-icon.png"),
            _ => null,
        };
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

            (_loadImage ??= GetLoadImageMI()).Invoke(null, [tex, data, false]);

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }

        static MethodInfo GetLoadImageMI()
        {
            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                 ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine");

            return t.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, [typeof(Texture2D), typeof(byte[]), typeof(bool)], null);
        }
    }

    public class APSongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry, bool IsHinted, string context = "library") : SongViewType(musicLibrary, songEntry, context)
    {
        public override string GetPrimaryText(bool selected)
        {
            SortString str = SongEntry.Name;
            if (IsHinted)
                return $"* {BaseViewType.FormatAs(str, TextType.Primary, selected)}";
            return BaseViewType.FormatAs(str, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            SortString str = SongEntry.Artist;
            return BaseViewType.FormatAs(str, TextType.Secondary, selected);
        }

        public override Sprite? GetIcon()
        {
            SortString str = SongEntry.Source;
            return SongSources.SourceToIcon(str);
        }
    }
}
