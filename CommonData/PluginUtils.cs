using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
#if !CLONE_HERO
using YARG.Core.Song;
using YARG.Gameplay;
#endif
using static YargArchipelagoCommon.APWorldData;

namespace YargArchipelagoCommon
{
    public static class PluginUtils
    {
        public static string ToColoredString(this string message, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>";

        public static string ToColoredString(this bool val) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(val ? Color.green : Color.red)}>{val}</color>";

        public static SongEntry GetSongEntry(this BaseAPSong song, APConnectionContainer container) =>
            song.HasSongEntry(container, out var entry) ? entry : null;

        public static bool HasSongEntry(this BaseAPSong song, APConnectionContainer container, out SongEntry entry) =>
            container.SongHashLookup.TryGetValue(song.GetActiveHash(container), out entry);

        public static string GetDisplayName(this BaseAPSong song, APConnectionContainer container, bool includePool)
        {
            var name = song.HasSongEntry(container, out var entry)
                ? EngineActions.GetSongDisplayName(entry) : song.GetActiveHash(container);
            return includePool ? $"[{song.PoolName}] {name}" : name;
        }

        public static bool CouldProductLocationCheck(this GameManager song, APConnectionContainer container, out IEnumerable<SongAPData> APSongEntries)
        {
            var hash = EngineActions.GetSongHash(song);
            APSongEntries = container.SlotData.Songs.Where(x =>
                EngineActions.SongHashComparer.Equals(x.GetActiveHash(container), hash) &&
                x.HasAvailableLocations(container) && x.IsSongUnlocked(container));
            return APSongEntries.Any();
        }
    }

    public static partial class APToastManager
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

        public static void ToastMessage(string text, Action onClick = null) => AddToast(APToastType.General, text, onClick);
        public static void ToastInformation(string text, Action onClick = null) => AddToast(APToastType.Information, text, onClick);
        public static void ToastSuccess(string text, Action onClick = null) => AddToast(APToastType.Success, text, onClick);
        public static void ToastWarning(string text, Action onClick = null) => AddToast(APToastType.Warning, text, onClick);
        public static void ToastError(string text, Action onClick = null) => AddToast(APToastType.Error, text, onClick);
        public static void ToastJunkItem(string text, Action onClick = null) => AddToast(APToastType.Junk, text, onClick);
        public static void ToastUsefulItem(string text, Action onClick = null) => AddToast(APToastType.Useful, text, onClick);
        public static void ToastProgressionItem(string text, Action onClick = null) => AddToast(APToastType.Progression, text, onClick);
        public static void ToastTrapItem(string text, Action onClick = null) => AddToast(APToastType.Trap, text, onClick);

        private static Sprite GetIcon(APToastType type)
        {
            var icon = type switch
            {
                APToastType.Junk or APToastType.Trap => APAssets.APIcon.Blue,
                APToastType.Useful or APToastType.Progression => APAssets.APIcon.Color,
                _ => APAssets.APIcon.White
            };
            return APAssets.Get(icon);
        }
    }

    public static partial class APAssets
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

            LoadImage(tex, data);

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }


    }

    public class SeedInfoMessage
    {
        public string Title { get; }
        public string Body { get; }

        public SeedInfoMessage(string title, string body)
        {
            Title = title;
            Body = body;
        }
    }

    public static class SeedInfo
    {
        public static int CountGoalItems(APConnectionContainer container, StaticItems type) =>
            container.ApItemsRecieved.Count(item => item.Type == type);

        public static SeedInfoMessage GetGoalConditionStatus(APConnectionContainer container)
        {
            var title = container.SlotData.GoalData.IsSongUnlocked(container)
                ? "Goal Conditions Met!\nPlay your goal song to complete the seed!"
                : "Goal Conditions NOT Met!\nComplete all the conditions below to unlock your goal song!";
            var conditions = new StringBuilder();
            if (container.GoalItemInPool(out var found, out _))
                conditions.AppendLine("Find Your Goal Unlock Item.").AppendLine($"Found: {found}").AppendLine();
            if (container.SlotData.SetlistNeededForGoal > 0)
                conditions.AppendLine($"Complete {container.SlotData.SetlistNeededForGoal} Songs.")
                    .AppendLine($"Current Completion {CountGoalItems(container, StaticItems.SongCompletion)}").AppendLine();
            if (container.SlotData.FamePointsForGoal > 0)
                conditions.AppendLine($"Find {container.SlotData.FamePointsForGoal} Fame Points.")
                    .AppendLine($"Current Fame {CountGoalItems(container, StaticItems.FamePoint)}").AppendLine();
            return new SeedInfoMessage(title, conditions.ToString());
        }

        public static SeedInfoMessage GetGoalReceiveMessage(APConnectionContainer container)
        {
            container.GoalItemInPool(out var received, out var info);
            if (!received)
                return new SeedInfoMessage("Goal Item", "Your goal song unlock item has not been found!");
            var session = container.GetSession();
            var player = session.Players.GetPlayerInfo(session.Players.ActivePlayer.Team, info.SendingPlayerSlot);
            var location = session.Locations.GetLocationNameFromId(info.SendingPlayerLocation, info.SendingPlayerGame);
            return new SeedInfoMessage("Goal Unlock Item Found!", $"Found by Player:\n{player.Name}\n\nFrom Location:\n{location}\n\nPlaying Game:\n{player.Game}");
        }

        public static SeedInfoMessage GetPoolInfo(APConnectionContainer container, string poolName)
        {
            var pool = container.SlotData.Pools[poolName];
            return GetPoolInfo(container, $"SONG POOL: {poolName}", pool);
        }

        public static SeedInfoMessage GetPoolInfo(APConnectionContainer container, string title, SongPool pool)
        {
            var requirements = pool.completion_requirements;
            var result = new StringBuilder()
                .AppendLine($"REQUIRED INSTRUMENT: {pool.instrument.GetDescription()}").AppendLine()
                .AppendLine("REWARD 1 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {requirements.reward1_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {requirements.reward1_req.GetDescription()}").AppendLine()
                .AppendLine("REWARD 2 REQUIREMENTS:")
                .AppendLine($"Minimum Difficulty: {requirements.reward2_diff.GetDescription()}")
                .AppendLine($"Minimum Score: {requirements.reward2_req.GetDescription()}");
            if (container.ReceivedInstruments.TryGetValue(pool.instrument, out var info))
            {
                var player = info.GetPlayerInfo(container);
                result.AppendLine().AppendLine($"{pool.instrument.GetDescription()} Recieved from").Append(player.Name);
                if (player.Slot > 0)
                {
                    var location = container.GetSession().Locations.GetLocationNameFromId(info.SendingPlayerLocation, player.Game);
                    result.AppendLine($" Playing {player.Game}").AppendLine($"at {location}");
                }
            }
            return new SeedInfoMessage(title, result.ToString());
        }


        public static SeedInfoMessage GetGoalProgress(int current, int needed, string name) =>
            new SeedInfoMessage($"{name} goal {(current < needed ? "not met" : "met")}!", $"Has: {current}\nNeed:{needed}");
    }


    public static class FillerItems
    {
        public static bool TryUseFailPrevention(APConnectionContainer container, Action preventFail)
        {
            var item = container.ApItemsRecieved.FirstOrDefault(x =>
                x.Type == StaticItems.FailPrevention && !container.seedConfig.ApItemsUsed.Contains(x));
            if (item == null) return false;
            preventFail();
            APToastManager.ToastSuccess($"{item.GetPlayerInfo(container).Name} cheered you on!");
            container.seedConfig.ApItemsUsed.Add(item);
            container.seedConfig.Save();
            return true;
        }


        public static BaseAPSong[] GetEditableSongs(APConnectionContainer container)
        {
            if (!container.IsSessionConnected || container.seedConfig == null) return Array.Empty<BaseAPSong>();
            var songs = new HashSet<BaseAPSong>(container.SlotData.Songs.Where(song => song.VisableInSongMenu(container)));
            if (container.SlotData.GoalData.VisableInSongMenu(container)) songs.Add(container.SlotData.GoalData);
            return songs.ToArray();
        }

        public static bool CanUseItem(APConnectionContainer container, StaticYargAPItem item, BaseAPSong song) =>
            container.IsSessionConnected && container.seedConfig != null &&
            !container.seedConfig.ApItemsUsed.Contains(item) && container.GetAllAquiredActionItems().Contains(item) &&
            GetEditableSongs(container).Contains(song);

        public static bool CanLowerRequirements(CompletionRequirements requirements) =>
            requirements.reward1_diff > SupportedDifficulty.Easy || requirements.reward2_diff > SupportedDifficulty.Easy ||
            requirements.reward1_req > CompletionReq.Clear || requirements.reward2_req > CompletionReq.Clear;

        public static CompletionReq LowerScoreRequirement(CompletionReq requirement)
        {
            var lower = requirement - 1;
#if !CLONE_HERO
            if (lower == CompletionReq.SixStar) lower--;
#endif
            return lower;
        }

        public static SongEntry[] GetValidReplacements(APConnectionContainer container, BaseAPSong song, StaticItems type)
        {
            var pool = song.GetPool(container.SlotData);
            var usedSongs = new HashSet<string>(EngineActions.SongHashComparer);
            if (container.SlotData.SongsByInstrument.TryGetValue(pool.instrument, out var songs))
                foreach (var item in songs)
                {
                    usedSongs.Add(item.Hash);
                    if (item.HasProxy(container, out var proxyHash)) usedSongs.Add(proxyHash);
                }
            return EngineActions.GetSongLookup().Where(entry =>
                EngineActions.GetSongExportData(entry.Value).TryGetDifficulty(pool.instrument, out var difficulty) &&
                (type == StaticItems.SwapPick || difficulty >= pool.min_difficulty && difficulty <= pool.max_difficulty) &&
                !usedSongs.Contains(entry.Key)).Select(entry => entry.Value).ToArray();
        }

        public static SeedInfoMessage SetRequirementOverride(APConnectionContainer container, StaticYargAPItem item,
            BaseAPSong song, CompletionRequirements requirements)
        {
            container.seedConfig.AdjustedDifficulties[song.UniqueKey] = requirements;
            container.seedConfig.ApItemsUsed.Add(item);
            container.seedConfig.Save();
            ArchipelagoEventManager.FlagSongLibraryForUpdate();
            return SeedInfo.GetPoolInfo(container, $"New Requirements for {song.GetDisplayName(container, false)}",
                new SongPool { instrument = song.GetPool(container.SlotData).instrument, completion_requirements = requirements });
        }

        public static SeedInfoMessage PerformSwap(APConnectionContainer container, StaticYargAPItem item,
            BaseAPSong song, SongEntry replacement)
        {
            var oldName = song.GetDisplayName(container, false);
            container.seedConfig.SongProxies[song.UniqueKey] = EngineActions.GetSongHash(replacement);
            container.seedConfig.ApItemsUsed.Add(item);
            container.seedConfig.Save();
            ArchipelagoEventManager.FlagSongLibraryForUpdate();
            return new SeedInfoMessage("Song Replaced",
                $"Replaced\n{oldName}\n\nwith\n{EngineActions.GetSongDisplayName(replacement)}\n\nIn Pool\n{song.PoolName}");
        }
    }

}
