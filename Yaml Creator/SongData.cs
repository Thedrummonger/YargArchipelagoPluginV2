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
                    Difficulties = core.Difficulties.ToDictionary(x => x.Key.ToString(), x => x.Value)
                };
            }
        }
        public class TaggedSongExportExtendedData : SongExportExtendedData
        {
            public TaggedSongExportExtendedData(SongExportExtendedData data, Func<SongExportExtendedData, string> Display) : base(data.core)
            {
                display = Display(data);
            }
            private readonly string display;
            public override string ToString() => display;
        }
    }
}
