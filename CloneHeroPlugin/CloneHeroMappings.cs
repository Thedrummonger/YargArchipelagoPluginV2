global using SongSection = ObjectPublicStLi1SoBoInStInInUnique;
global using GroupSongStats = ObjectPublicInBoInDoBoByInObInBoUnique;
global using SongScanEnumerator = SongScan.ObjectNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObSoBofuBoObObObUnique;
global using CloneHeroInstrument = EnumPublicSealedvaGuBaRhSiGuDrSiKePrUnique;
global using CloneHeroDifficulty = EnumPublicSealedvaNoEaMeHaExCo7vUnique;
global using ToastType = EnumPublicSealedvaIn2vUnique;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.XrefScans;
using UnityEngine;
using SongLibrary = ObjectPublicAbstractSealedLi1SoDi2ObInLi1SoUnique;
using SongSections = Il2CppSystem.Collections.Generic.List<ObjectPublicStLi1SoBoInStInInUnique>;
using PlayerSlot = ObjectPublicObPlInPlBoBoObBoObBoUnique;
using PlayerProfile = Object1PublicObBoObStBoObObObObUnique;
using PlayerStats = ObjectPublicInSiInDoObInBoByInBoUnique;
using BaseEngine = ObjectPublicAbstractDoBoDoInBoObDoInSiBoUnique;
using GameSettings = ObjectPublicAbstractSealedBoObObObObObObObObObUnique;

namespace YargArchipelagoCommon
{
    internal static class CloneHeroMappings
    {
        private sealed record PlatformMapping(string MainMenuSelection, string RefreshSongList, string SelectMenuIndex, string EndSong, string SongListRender);

        private static readonly PlatformMapping Windows = new(
            MainMenuSelection: "Method_Public_Virtual_Void_1",
            RefreshSongList: "Method_Public_Virtual_Void_11",
            SelectMenuIndex: "Method_Public_Virtual_New_Void_Int32_Boolean_1",
            EndSong: "Method_Public_Void_4",
            SongListRender: "Method_Protected_Virtual_Void_0");

        private static readonly PlatformMapping Linux = new(
            MainMenuSelection: "Method_Public_Virtual_Void_4",
            RefreshSongList: "Method_Public_Virtual_Void_14",
            SelectMenuIndex: "Method_Public_Virtual_New_Void_Int32_Boolean_0",
            EndSong: "Method_Public_Void_3",
            SongListRender: "Method_Protected_Virtual_Void_0");

        private static readonly PlatformMapping MacOS = new(
            MainMenuSelection: "Method_Public_Virtual_Void_3",
            RefreshSongList: "Method_Public_Virtual_Void_8",
            SelectMenuIndex: "Method_Public_Virtual_New_Void_Int32_Boolean_2",
            EndSong: "Method_Public_Void_6",
            SongListRender: "Method_Protected_Virtual_Void_1");

        public static MethodInfo MainMenuSelection { get; private set; }
        public static MethodInfo MainMenuLeaderboardToggle { get; private set; }
        public static MethodInfo GetLeaderboardMode { get; private set; }
        public static MethodInfo SetLeaderboardMode { get; private set; }
        public static MethodInfo SongSectionsReindex { get; private set; }
        public static MethodInfo ToastNotification { get; private set; }
        public static MethodInfo SongListRender { get; private set; }
        public static Action<ScrollingText, string, bool> SetSongTitle { get; private set; }
        public static Func<SongSection, string> GetSectionName { get; private set; }
        public static Action<SongSelect> RefreshSongList { get; private set; }
        public static Action<BaseMenu, int, bool> SelectMenuIndex { get; private set; }
        public static Action<SongSections> ReindexSongSections { get; private set; }
        public static Action<string, string, ToastType, float> ShowToast { get; private set; }
        public static Func<PlayerSlot, bool> IsPlayerActive { get; private set; }
        public static Func<PlayerStats, bool> IsFullCombo { get; private set; }
        public static Func<GroupSongStats, int> GetBandScore { get; private set; }
        public static Action<BaseEngine, long, bool> GainStarPower { get; private set; }
        public static Func<BaseEngine, long> GetQuarterSpBar { get; private set; }
        public static Func<BaseEngine, float> GetRockMeter { get; private set; }
        public static Action<BaseEngine, float> SetRockMeter { get; private set; }
        public static MethodInfo RockMeterUpdated { get; private set; }
        public static Action<BasePlayer, float> UpdateRockMeter { get; private set; }
        public static Action<GameManager> EndSong { get; private set; }
        public static Action<FadeBehaviour, string> LoadScene { get; private set; }

        public static Il2CppReferenceArray<PlayerStats> GetStats(GroupSongStats groupStats) => groupStats?.field_Public_Il2CppReferenceArray_1_ObjectPublicInSiInDoObInBoByInBoUnique_0;
        public static PlayerProfile GetPlayerProfile(PlayerSlot player) => player.field_Public_Object1PublicObBoObStBoObObObObUnique_0;
        public static CloneHeroInstrument GetInstrument(PlayerProfile profile) => profile.field_Public_EnumPublicSealedvaGuBaRhSiGuDrSiKePrUnique_0;
        public static CloneHeroDifficulty GetDifficulty(PlayerProfile profile) => profile.field_Public_EnumPublicSealedvaNoEaMeHaExCo7vUnique_0;
        public static int GetStars(PlayerStats stats) => stats.field_Public_Int32_24;
        public static bool IsNoFailEnabled() => GameSettings.field_Public_Static_Object2PublicBoSiInSiDoStSiStStUnique_27.prop_Boolean_0;

        public static Il2CppSystem.Collections.Generic.List<SongEntry> GetLoadedSongs() => SongLibrary.field_Public_Static_List_1_SongEntry_0;
        public static SongSections GetSongSections() => SongLibrary.field_Public_Static_List_1_ObjectPublicStLi1SoBoInStInInUnique_0;
        public static Il2CppSystem.Collections.Generic.List<SongEntry> GetSectionSongs(SongSection section) => section.field_Public_List_1_SongEntry_0;
        public static bool IsSectionCollapsed(SongSection section) => section.field_Public_Boolean_0;
        public static void SetSectionCollapsed(SongSection section, bool collapsed) => section.field_Public_Boolean_0 = collapsed;
        public static int GetSectionStartIndex(SongSection section) => section.prop_Int32_0;
        public static int GetSectionEndIndex(SongSection section) => section.prop_Int32_1;
        public static SongScan GetSongScan(SongScanEnumerator scan) => scan.__4__this;

        public static int GetMenuScrollOffset(BaseMenu menu) => menu.field_Protected_Int32_0;
        public static int GetSelectedMenuIndex(this BaseMenu menu) => menu.field_Protected_Int32_1;
        public static Il2CppReferenceArray<ScrollingText> GetSongTitles(SongSelect menu) => menu.field_Private_Il2CppReferenceArray_1_ScrollingText_0;
        public static TMPro.TextMeshProUGUI GetSongTitleText(ScrollingText title) => title.field_Private_TextMeshProUGUI_0;
        public static NotificationPopups GetNotificationPopup() => NotificationPopups.field_Private_Static_NotificationPopups_0;

        public static void Initialize()
        {
            if (MainMenuSelection != null)
                return;

            var mapping = (Application.platform, RuntimeInformation.ProcessArchitecture) switch
            {
                (RuntimePlatform.WindowsPlayer, Architecture.X64) => Windows,
                (RuntimePlatform.LinuxPlayer, Architecture.X64) => Linux,
                (RuntimePlatform.OSXPlayer, Architecture.X64) => MacOS,
                _ => throw new PlatformNotSupportedException($"Clone Hero Archipelago only supports Windows x64, Linux x64, and macOS x64. Current platform: {Application.platform}, {RuntimeInformation.ProcessArchitecture}.")
            };

            var selection = GetMethod(typeof(MainMenu), mapping.MainMenuSelection, typeof(void), Type.EmptyTypes);
            var refresh = GetMethod(typeof(SongSelect), mapping.RefreshSongList, typeof(void), Type.EmptyTypes);

            GetLeaderboardMode = GetMethod(typeof(LeaderboardsOnlineManager), "Method_Public_Static_get_Boolean_0", typeof(bool), Type.EmptyTypes, true);
            MainMenuLeaderboardToggle = XrefScanner.UsedBy(GetLeaderboardMode)
                .Where(xref => xref.Type == XrefType.Method)
                .Select(xref => xref.TryResolve()).OfType<MethodInfo>().Distinct()
                .Single(method => method.DeclaringType == typeof(MainMenu) &&
                    method.Name.StartsWith("Method_Private_Void_") && !method.Name.Contains("_PDM_") &&
                    method.GetParameters().Length == 0);
            SetLeaderboardMode = typeof(LeaderboardsOnlineManager).GetMethods()
                .Where(method => method.ReturnType == typeof(void) && method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == typeof(bool))
                .Single(method => XrefScanner.UsedBy(method)
                    .Any(xref => xref.Type == XrefType.Method && xref.TryResolve() == MainMenuLeaderboardToggle));

            GainStarPower = GetMethod(typeof(BaseEngine), "Method_Public_Virtual_New_Void_Int64_Boolean_0", typeof(void), [typeof(long), typeof(bool)]).CreateDelegate<Action<BaseEngine, long, bool>>();
            GetQuarterSpBar = GetPropertyGetter<Func<BaseEngine, long>>(typeof(BaseEngine), "field_Public_Int64_4");
            GetRockMeter = GetPropertyGetter<Func<BaseEngine, float>>(typeof(BaseEngine), "field_Protected_Single_0");
            SetRockMeter = typeof(BaseEngine).GetProperty("field_Protected_Single_0").SetMethod.CreateDelegate<Action<BaseEngine, float>>();
            RockMeterUpdated = GetMethod(typeof(BasePlayer), "Method_Public_Virtual_New_Void_Single_0", typeof(void), [typeof(float)]);
            UpdateRockMeter = RockMeterUpdated.CreateDelegate<Action<BasePlayer, float>>();
            EndSong = GetMethod(typeof(GameManager), mapping.EndSong, typeof(void), Type.EmptyTypes).CreateDelegate<Action<GameManager>>();
            LoadScene = GetMethod(typeof(FadeBehaviour), "Method_Public_Void_String_0", typeof(void), [typeof(string)]).CreateDelegate<Action<FadeBehaviour, string>>();
            SongListRender = GetMethod(typeof(SongSelect), mapping.SongListRender, typeof(void), Type.EmptyTypes);
            SetSongTitle = GetMethod(typeof(ScrollingText), "Method_Public_Void_String_Boolean_0", typeof(void), [typeof(string), typeof(bool)]).CreateDelegate<Action<ScrollingText, string, bool>>();

            var selectIndex = GetMethod(typeof(BaseMenu), mapping.SelectMenuIndex, typeof(void), [typeof(int), typeof(bool)]);
            var reindex = GetMethod(typeof(SongLibrary), "Method_Private_Static_Void_List_1_ObjectPublicStLi1SoBoInStInInUnique_PDM_0", typeof(void), [typeof(SongSections)], true);
            var toast = GetMethod(typeof(NotificationPopups), "Method_Public_Static_Void_String_String_EnumPublicSealedvaIn2vUnique_Single_0", typeof(void), [typeof(string), typeof(string), typeof(ToastType), typeof(float)], true);

            var getSectionName = GetPropertyGetter<Func<SongSection, string>>(typeof(SongSection), "prop_String_0");
            var refreshSongList = refresh.CreateDelegate<Action<SongSelect>>();
            var selectMenuIndex = selectIndex.CreateDelegate<Action<BaseMenu, int, bool>>();
            var reindexSongSections = reindex.CreateDelegate<Action<SongSections>>();
            var showToast = toast.CreateDelegate<Action<string, string, ToastType, float>>();
            var isPlayerActive = GetPropertyGetter<Func<PlayerSlot, bool>>(typeof(PlayerSlot), "prop_Boolean_0");
            var isFullCombo = GetPropertyGetter<Func<PlayerStats, bool>>(typeof(PlayerStats), "prop_Boolean_0");
            var getBandScore = GetPropertyGetter<Func<GroupSongStats, int>>(typeof(GroupSongStats), "field_Public_Int32_2");

            GetSectionName = getSectionName;
            RefreshSongList = refreshSongList;
            SelectMenuIndex = selectMenuIndex;
            ReindexSongSections = reindexSongSections;
            ShowToast = showToast;
            IsPlayerActive = isPlayerActive;
            IsFullCombo = isFullCombo;
            GetBandScore = getBandScore;
            SongSectionsReindex = reindex;
            ToastNotification = toast;
            MainMenuSelection = selection;
        }

        private static T GetPropertyGetter<T>(Type type, string name) where T : Delegate
        {
            var getter = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetMethod
                ?? throw new MissingMemberException(type.FullName, name);
            return getter.CreateDelegate<T>();
        }

        private static MethodInfo GetMethod(Type type, string name, Type returnType, Type[] parameters, bool isStatic = false)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            var method = type.GetMethod(name, flags, null, parameters, null);
            if (method == null || method.ReturnType != returnType || method.ContainsGenericParameters)
                throw new MissingMethodException($"Failed to get mapping {returnType.Name} {type.Name}.{name}.");
            return method;
        }
    }
}
