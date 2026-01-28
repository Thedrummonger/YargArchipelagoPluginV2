using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YargArchipelagoCommon;
using YargArchipelagoPlugin;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public class Utility
    {
        public class DisplayItem<T>
        {
            public T Value { get; set; }
            public string Display { get; set; }

            public override string ToString()
            {
                return Display;
            }
        }

        // Helper method for enums using GetDescription
        public static List<DisplayItem<T>> GetEnumDataSource<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => new DisplayItem<T>
                {
                    Value = e,
                    Display = e.GetDescription()
                })
                .ToList();
        }

        // Generic helper method for any collection with custom display selector
        public static List<DisplayItem<T>> GetDataSource<T>(IEnumerable<T> items, Func<T, string> displaySelector)
        {
            return items.Select(item => new DisplayItem<T>
            {
                Value = item,
                Display = displaySelector(item)
            })
            .ToList();
        }

        public static SongPool NewSongPool(SupportedInstrument i, int a = 0, int min = 3, int max = 5)
        {
            return new SongPool
            {
                instrument = i,
                amount_in_pool = a,
                max_difficulty = max,
                min_difficulty = min,
                completion_requirements = new CompletionRequirements()
                {
                    reward1_diff = SupportedDifficulty.Expert,
                    reward2_diff = SupportedDifficulty.Expert,
                    reward1_req = CompletionReq.Clear,
                    reward2_req = CompletionReq.ThreeStar
                }
            };
        }

        public static string TryGetCrossPlatformSongDataFile()
        {
            string fileName = Path.GetFileName(CommonData.SongExportFile);

            // Try HOME environment variable. Might work in standard wine but I guess some containers like bottles does not expose this
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                string linuxConfigPath = Path.Combine("Z:" + home.Replace("/", "\\"), ".config", "YARChipelago");
                if (Directory.Exists(linuxConfigPath))
                {
                    string linuxFilePath = Path.Combine(linuxConfigPath, fileName);
                    if (File.Exists(linuxFilePath))
                        return linuxFilePath;
                }
            }

            // Try Z:\home scan
            string username = Environment.UserName;
            if (!string.IsNullOrEmpty(username))
            {
                string linuxConfigPath = Path.Combine("Z:\\home", username, ".config", "YARChipelago");
                if (Directory.Exists(linuxConfigPath))
                {
                    string linuxFilePath = Path.Combine(linuxConfigPath, fileName);
                    if (File.Exists(linuxFilePath))
                        return linuxFilePath;
                }
            }

            // Standard Windows AppData path
            if (File.Exists(CommonData.SongExportFile))
                return CommonData.SongExportFile;

            return null;
        }
    }
}
