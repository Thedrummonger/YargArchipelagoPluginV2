using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Yaml_Creator.SongData;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    internal class Tests
    {
        public static void WriteTestYaml(IEnumerable<SongExportExtendedData> ExportFile)
        {
            var TestYAML = new YAMLCore();
            TestYAML.name = "TDMYarg";
            TestYAML.YAYARG.songList = SongDataConverter.ConvertSongDataToBase64(ExportFile);
            TestYAML.YAYARG.song_pools["Guitar"] = Utility.NewSongPool(SupportedInstrument.FiveFretGuitar, 20, 2, 5);
            TestYAML.YAYARG.song_pools["Drums"] = Utility.NewSongPool(SupportedInstrument.ProDrums, 20, 1, 4);
            TestYAML.YAYARG.song_pools["Bass"] = Utility.NewSongPool(SupportedInstrument.FiveFretBass, 5, 5, 6);
            YAMLWriter.WriteToFile(TestYAML, "TDMYarg_Test.yaml");
        }
    }
}
