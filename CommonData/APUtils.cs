using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public static class APUtils
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

        public static string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength) + "...";
        }


        public static string ToColoredString(this LogMessage message)
        {
            var result = new StringBuilder();
            foreach (var i in message.Parts)
            {
                var hexColor = $"#{i.Color.R:X2}{i.Color.G:X2}{i.Color.B:X2}";
                result.Append($"<color={hexColor}>{i.Text}</color>");
            }
            return result.ToString();
        }

        public static string GetSongNameFromLocationString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int index = input.LastIndexOf("on ", StringComparison.Ordinal);
            return index >= 0 ? input.Substring(0, index) : input;
        }

        public static void ClearFilters() => _filterCache.Clear();
        private static readonly Dictionary<(Type, string), object> _filterCache = new Dictionary<(Type, string), object>();
        public static T[] FilterItems<T>(IEnumerable<T> Objects, string filterText, Func<T, string> GetDisplay)
        {
            var cacheKey = (typeof(T), filterText);
            if (_filterCache.TryGetValue(cacheKey, out var cachedResult))
                return (T[])cachedResult;

            var result = Objects
                .Where(s => GetDisplay(s).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(s => GetDisplay(s), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _filterCache[cacheKey] = result;

            return result;
        }
    }

    public class APSongResult
    {
        public string Hash { get; set; }
        public string Name { get; set; }
        public bool IsPractice { get; set; }
        public bool PlayerHasFailed { get; set; }
        public long BandScore { get; set; }
        public List<APPlayerResult> Players { get; set; } = new List<APPlayerResult>();
    }

    public class APPlayerResult
    {
        public SupportedInstrument? CurrentInstrument { get; set; }
        public SupportedDifficulty CurrentDifficulty { get; set; }
        public double Stars { get; set; }
        public bool IsSixStars { get; set; }
        public bool IsGoldStars { get; set; }
        public bool IsFc { get; set; }
    }

    public static class SongCheckRequirements
    {
        public static bool PlayedValidInsturmentForCheck(SupportedInstrument? played, SupportedInstrument required)
        {
            if (played == required) return true;
            bool baseDrums = played == SupportedInstrument.FourLaneDrums || played == SupportedInstrument.FiveLaneDrums;
            bool requiresBaseDrums = required == SupportedInstrument.FourLaneDrums || required == SupportedInstrument.FiveLaneDrums;
            return (baseDrums || played == SupportedInstrument.ProDrums) && requiresBaseDrums;
        }

        public static bool MetStandardCheckRequirement(this IEnumerable<APPlayerResult> Players, BaseAPSong song, BaseConnectionContainer container, out bool DeathLink) =>
            MetSongCheckRequirements(Players, song, container, false, out DeathLink);
        public static bool MetExtraCheckRequirement(this IEnumerable<APPlayerResult> Players, BaseAPSong song, BaseConnectionContainer container, out bool DeathLink) =>
            MetSongCheckRequirements(Players, song, container, true, out DeathLink);
        public static bool MetAllCheckRequirments(this IEnumerable<APPlayerResult> Players, BaseAPSong song, BaseConnectionContainer container, out bool DeathLink)
        {
            var MetStandard = MetStandardCheckRequirement(Players, song, container, out bool DLS);
            var MetExtra = MetExtraCheckRequirement(Players, song, container, out bool DLE);
            DeathLink = DLS || DLE;
            return MetStandard && MetExtra;
        }
        public static bool MetSongCheckRequirements(this IEnumerable<APPlayerResult> Players, BaseAPSong song, BaseConnectionContainer container, bool ExtraCheck, out bool DeathLink)
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
                if (!PlayedValidInsturmentForCheck(player.CurrentInstrument, pool.instrument)) continue;
                if (player.CurrentDifficulty < diff) continue;
                HadValidPlayer = true;
                bool metRequirement = req switch
                {
                    CompletionReq.Clear => true,
                    CompletionReq.OneStar => player.Stars >= 1,
                    CompletionReq.TwoStar => player.Stars >= 2,
                    CompletionReq.ThreeStar => player.Stars >= 3,
                    CompletionReq.FourStar => player.Stars >= 4,
                    CompletionReq.FiveStar => player.Stars >= 5,
                    CompletionReq.SixStar => player.IsSixStars,
                    CompletionReq.GoldStar => player.IsGoldStars,
                    CompletionReq.FullCombo => player.IsFc,
                    _ => false
                };
                if (!metRequirement) continue;
                DeathLink = false;
                return true;
            }
            DeathLink = HadValidPlayer;
            return false;
        }
    }

    public static class ExtraAPFunctionalityHelper
    {
        public const long minEnergyLinkScale = 10_000;
        public const long maxEnergyLinkScale = 500_000;
        public static Dictionary<StaticItems, long> PriceDict = new Dictionary<StaticItems, long>
        {
            { StaticItems.SwapRandom, 15_000_000_000 },
            { StaticItems.SwapPick, 17_000_000_000 },
            { StaticItems.LowerDifficulty, 16_000_000_000 }
        };

        public static string EnergyLinkKey(ArchipelagoSession session) => $"EnergyLink{session.Players.ActivePlayer.Team}";
        public static bool TryPurchaseItem(BaseConnectionContainer container, StaticItems Type)
        {
            if (!PriceDict.TryGetValue(Type, out var Price))
                return false;
            if (!TryUseEnergy(container, Price))
                return false;
            AddPurchasedItem(container, Type);
            return true;
        }

        private static void AddPurchasedItem(BaseConnectionContainer container, StaticItems Type)
        {
            var CurCount = container.seedConfig.ApItemsPurchased.Where(x => x.Type == Type).Count();
            container.seedConfig.ApItemsPurchased.Add(new StaticYargAPItem(Type, StaticItemIDbyValue[Type], -99, CurCount, APWorldData.Game));
            container.seedConfig.Save();
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
        public static void SendScoreAsEnergy(BaseConnectionContainer container, long BaseScore, bool WasLocationChecked)
        {
            if (!container.IsSessionConnected || container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled) return;
            if (container.seedConfig.EnergyLinkMode == EnergyLinkType.check_song && !WasLocationChecked) return;
            if (container.seedConfig.EnergyLinkMode == EnergyLinkType.other_song && WasLocationChecked) return;

            var Session = container.GetSession();
            Session.DataStorage[EnergyLinkKey(Session)].Initialize(0);
            Session.DataStorage[EnergyLinkKey(Session)] += ScaleEnergyValue(container, BaseScore);
        }

        public static long ScaleEnergyValue(BaseConnectionContainer container, long baseAmount)
        {
            int AmountOfLocationsTotal = container.GetSession().Locations.AllLocations.Count;
            int AmountOfLocationsChecked = container.GetSession().Locations.AllLocationsChecked.Count;
            double completionPercentage = AmountOfLocationsChecked / AmountOfLocationsTotal;
            double scale = minEnergyLinkScale + (completionPercentage * (maxEnergyLinkScale - minEnergyLinkScale));
            long Energy = (long)(baseAmount * scale);
            return Energy;
        }

        public static long GetEnergy(BaseConnectionContainer container)
        {
            if (!container.IsSessionConnected || container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled) return 0;
            var Session = container.GetSession();
            Session.DataStorage[EnergyLinkKey(Session)].Initialize(0);
            return Session.DataStorage[EnergyLinkKey(Session)];
        }

        public static bool TryUseEnergy(BaseConnectionContainer container, long Amount)
        {
            if (!container.IsSessionConnected || container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled) return false;
            return SpendEnergy(container.GetSession(), GetEnergy(container), Amount);
        }

        private static bool SpendEnergy(ArchipelagoSession session, long available, long amount)
        {
            if (available < amount) return false;
            session.DataStorage[EnergyLinkKey(session)] -= amount;
            return true;
        }

        public static Task<bool> TryPurchaseItemAsync(BaseConnectionContainer container, StaticItems type, Action<Action> queueMainThread)
        {
            if (container.IsPurchasingItem || !container.IsSessionConnected ||
                container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled || !PriceDict.TryGetValue(type, out var price))
                return Task.FromResult(false);
            var settings = container.seedConfig;
            var session = container.GetSession();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            container.IsPurchasingItem = true;
            var spend = TryUseEnergyAsync(container, price);
            _ = Task.Run(async () =>
            {
                bool purchased = false;
                Exception error = null;
                try { purchased = await spend.ConfigureAwait(false); }
                catch (Exception e) { error = e; }
                queueMainThread(() =>
                {
                    try
                    {
                        if (error != null) throw error;
                        if (!container.IsSessionConnected || !ReferenceEquals(session, container.GetSession()) ||
                            !ReferenceEquals(settings, container.seedConfig)) purchased = false;
                        if (purchased)
                        {
                            AddPurchasedItem(container, type);
                        }
                        completion.SetResult(purchased);
                    }
                    catch (Exception e) { completion.SetException(e); }
                    finally { container.IsPurchasingItem = false; }
                });
            });
            return completion.Task;
        }

        public static Task<long> GetEnergyAsync(BaseConnectionContainer container)
        {
            if (!container.IsSessionConnected || container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled)
                return Task.FromResult(0L);
            var session = container.GetSession();
            return Task.Run(async () =>
            {
                await container.EnergyLinkRequests.WaitAsync().ConfigureAwait(false);
                try
                {
                    session.DataStorage[EnergyLinkKey(session)].Initialize(0);
                    var request = session.DataStorage[EnergyLinkKey(session)].GetAsync<long>();
                    if (await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false) != request)
                        throw new TimeoutException();
                    return await request.ConfigureAwait(false);
                }
                finally { container.EnergyLinkRequests.Release(); }
            });
        }

        public static async Task<bool> TryUseEnergyAsync(BaseConnectionContainer container, long amount)
        {
            if (!container.IsSessionConnected || container.seedConfig.EnergyLinkMode <= EnergyLinkType.disabled) return false;
            var session = container.GetSession();
            var settings = container.seedConfig;
            var energy = await GetEnergyAsync(container).ConfigureAwait(false);
            if (!container.IsSessionConnected || !ReferenceEquals(session, container.GetSession()) ||
                !ReferenceEquals(settings, container.seedConfig) || settings.EnergyLinkMode <= EnergyLinkType.disabled) return false;
            return SpendEnergy(session, energy, amount);
        }
    }
}
