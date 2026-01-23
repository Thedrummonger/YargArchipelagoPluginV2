using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YARG.Core.Audio;
//Don't Let visual studios lie to me these are needed
using YARG.Core.Engine;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
//----------------------------------------------------
using YARG.Gameplay.Player;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YargArchipelagoCommon;

namespace YargArchipelagoPlugin
{
    public static class YargEngineActions
    {
        public static void DumpAvailableSongs(SongCache SongCache)
        {
            Dictionary<string, CommonData.SongData> SongData = new Dictionary<string, CommonData.SongData>();

            foreach (var instrument in SongCache.Instruments)
            {
                if (!YargAPUtils.IsSupportedInstrument(instrument.Key, out var supportedInstrument))
                    continue;
                foreach (var Difficulty in instrument.Value)
                {
                    if (Difficulty.Key < 0)
                        continue;
                    foreach (var song in Difficulty.Value)
                    {
                        var data = YargAPUtils.ToSongData(song);
                        if (!SongData.ContainsKey(data.SongChecksum))
                            SongData[data.SongChecksum] = data;
                        SongData[data.SongChecksum].Difficulties[supportedInstrument.Value] = Difficulty.Key;
                    }
                }
            }
            if (!Directory.Exists(CommonData.DataFolder)) Directory.CreateDirectory(CommonData.DataFolder);
            File.WriteAllText(CommonData.SongExportFile, JsonConvert.SerializeObject(SongData.Values.ToArray(), Formatting.Indented));
        }

        public static int GetListViewIndex(List<ViewType> listView, string Key)
        {
            string allSongsKey = Localize.Key("Menu.MusicLibrary.AllSongs");
            var primaryField = AccessTools.Field(typeof(CategoryViewType), "_primary");
            int insertIndex = -1;
            for (int i = 0; i < listView.Count; i++)
            {
                if (listView[i] is CategoryViewType cat && (string)primaryField.GetValue(cat) == allSongsKey)
                {
                    insertIndex = i;
                    break;
                }
            }
            return insertIndex;
        }

        public static void InsertAPListViewSongs(MusicLibraryMenu menu, List<ViewType> listView, IEnumerable<(SongEntry song, string Instrument)> entries)
        {
            int insertIndex = GetListViewIndex(listView, "Menu.MusicLibrary.AllSongs");
            var groups = entries.GroupBy(t => t.Instrument, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in groups)
            {
                var songs = grp.Select(t => t.song).OrderBy<SongEntry, string>(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                string header = $"Archipelago Songs: {grp.Key}".ToUpper();
                listView.Insert(insertIndex++, new CategoryViewType(header, songs.Length, songs, menu.RefreshAndReselect));

                foreach (var song in songs)
                    listView.Insert(insertIndex++, new SongViewType(menu, song));
            }
        }
    }
}
