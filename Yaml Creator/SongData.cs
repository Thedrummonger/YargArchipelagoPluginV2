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
            public HashSet<string> ExcludedPools = new HashSet<string>();
            public HashSet<string> IncludedPools = new HashSet<string>();
            public SongExportExtendedData(SongExportData data)
            {
                core = data;
            }
            public override string ToString()
            {
                return $"{core.Name} by {core.Artist}";
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
    }
}
