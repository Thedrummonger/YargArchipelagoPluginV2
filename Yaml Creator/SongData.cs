using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public class SongData
    {
        public class SongExportExtendedData
        {
            public SongExportData core;
            public string DisplayOverride = null;
            public SongExportExtendedData(SongExportData data)
            {
                core = data;
            }
            public override string ToString()
            {
                return DisplayOverride ?? $"{core.Name} by {core.Artist}";
            }
            public SongDataConverter.CompressedSongData Compress()
            {
                return new SongDataConverter.CompressedSongData
                {
                    Title = ToString(),
                    Time = Math.Round(core.Time),
                    Difficulties = core.Difficulties.ToDictionary(x => x.Key.ToString(), x => x.Value)
                };
            }
        }
    }
}
