using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public class CrossPlatformFileLoader
    {
        public class SearchResult
        {
            public bool IsLinuxEnvironment { get; set; }
            public bool LinuxConfigPathExists { get; set; }
            public bool LinuxFileExists { get; set; }
            public string LinuxPathSearched { get; set; }
            public bool WindowsConfigPathExists { get; set; }
            public bool WindowsFileExists { get; set; }
            public string WindowsPathSearched { get; set; }
            public string FoundFilePath { get; set; }
            public SongExportData[] ParsedData { get; set; }
            public bool ParseSuccessful { get; set; }
            public string ParseError { get; set; }
        }

        public static SongExportData[] LoadSongDataCrossPlatform()
        {
            var result = SearchForFile();

            if (result.FoundFilePath == null)
            {
                ShowFileNotFoundError(result);
                return null;
            }

            result.ParsedData = TryParseFile(result.FoundFilePath, out bool parseSuccessful, out string parseError);
            result.ParseSuccessful = parseSuccessful;
            result.ParseError = parseError;

            if (!result.ParseSuccessful)
            {
                ShowParseError(result);
                return null;
            }

            //ShowDebugInfo(result);

            return result.ParsedData;
        }

        private static SearchResult SearchForFile()
        {
            var result = new SearchResult
            {
                WindowsPathSearched = CommonData.SongExportFile,
                WindowsConfigPathExists = Directory.Exists(Path.GetDirectoryName(CommonData.SongExportFile)),
                WindowsFileExists = File.Exists(CommonData.SongExportFile)
            };

            string fileName = Path.GetFileName(CommonData.SongExportFile);

            TryHomeEnvironmentVariable(result, fileName);
            if (result.FoundFilePath != null)
                return result;

            TryZDrivePath(result, fileName);
            if (result.FoundFilePath != null)
                return result;

            if (result.WindowsFileExists)
            {
                result.FoundFilePath = CommonData.SongExportFile;
                return result;
            }

            return result;
        }

        private static void TryHomeEnvironmentVariable(SearchResult result, string fileName)
        {
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home))
                return;

            result.IsLinuxEnvironment = true;
            string linuxConfigPath = Path.Combine("Z:" + home.Replace("/", "\\"), ".config", "YARChipelago");
            result.LinuxPathSearched = linuxConfigPath;
            result.LinuxConfigPathExists = Directory.Exists(linuxConfigPath);

            if (!result.LinuxConfigPathExists)
                return;

            string linuxFilePath = Path.Combine(linuxConfigPath, fileName);
            result.LinuxFileExists = File.Exists(linuxFilePath);

            if (result.LinuxFileExists)
                result.FoundFilePath = linuxFilePath;
        }

        private static void TryZDrivePath(SearchResult result, string fileName)
        {
            string username = Environment.UserName;
            if (string.IsNullOrEmpty(username))
                return;

            if (!Directory.Exists("Z:\\home"))
                return;

            result.IsLinuxEnvironment = true;
            string linuxConfigPath = Path.Combine("Z:\\home", username, ".config", "YARChipelago");
            result.LinuxPathSearched = linuxConfigPath;
            result.LinuxConfigPathExists = Directory.Exists(linuxConfigPath);

            if (!result.LinuxConfigPathExists)
                return;

            string linuxFilePath = Path.Combine(linuxConfigPath, fileName);
            result.LinuxFileExists = File.Exists(linuxFilePath);

            if (result.LinuxFileExists)
                result.FoundFilePath = linuxFilePath;
        }

        private static SongExportData[] TryParseFile(string filePath, out bool success, out string error)
        {
            try
            {
                string rawData = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<SongExportData[]>(rawData);
                success = true;
                error = null;
                return data;
            }
            catch (Exception ex)
            {
                success = false;
                error = ex.Message;
                return null;
            }
        }

        private static void ShowFileNotFoundError(SearchResult result)
        {
            var message = new System.Text.StringBuilder();
            message.AppendLine("ERROR: Song data file could not be found.");
            message.AppendLine();
            message.AppendLine("Ensure you have launched YARG at least once with the mod loaded.");
            message.AppendLine();

            if (result.IsLinuxEnvironment)
            {
                message.AppendLine("Searched locations:");
                message.AppendLine();

                if (!string.IsNullOrEmpty(result.LinuxPathSearched))
                {
                    message.AppendLine($"Linux Config Path: {result.LinuxPathSearched}");
                    message.AppendLine($"  Directory exists: {result.LinuxConfigPathExists}");
                    message.AppendLine($"  File exists: {result.LinuxFileExists}");
                    message.AppendLine();
                }

                message.AppendLine($"Windows Path: {result.WindowsPathSearched}");
                message.AppendLine($"  Directory exists: {result.WindowsConfigPathExists}");
                message.AppendLine($"  File exists: {result.WindowsFileExists}");
            }
            else
            {
                message.AppendLine($"Searched: {result.WindowsPathSearched}");
                message.AppendLine($"Directory exists: {result.WindowsConfigPathExists}");
                message.AppendLine($"  File exists: {result.WindowsFileExists}");
            }

            MessageBox.Show(message.ToString(), "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void ShowParseError(SearchResult result)
        {
            var message = new System.Text.StringBuilder();
            message.AppendLine("ERROR: Song data file could not be parsed.");
            message.AppendLine();
            message.AppendLine($"File location: {result.FoundFilePath}");
            message.AppendLine();
            message.AppendLine($"Error: {result.ParseError}");
            message.AppendLine();
            message.AppendLine("The file may be corrupted. Try launching YARG again to regenerate it.");

            MessageBox.Show(message.ToString(), "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void ShowDebugInfo(SearchResult result)
        {
            MessageBox.Show($"IsLinuxEnvironment: {result.IsLinuxEnvironment}\n" +
                            $"LinuxPathSearched: {result.LinuxPathSearched ?? "null"}\n" +
                            $"LinuxConfigPathExists: {result.LinuxConfigPathExists}\n" +
                            $"LinuxFileExists: {result.LinuxFileExists}\n" +
                            $"WindowsPathSearched: {result.WindowsPathSearched}\n" +
                            $"WindowsConfigPathExists: {result.WindowsConfigPathExists}\n" +
                            $"WindowsFileExists: {result.WindowsFileExists}\n" +
                            $"FoundFilePath: {result.FoundFilePath ?? "null"}",
                            "Debug - Search Result");
        }
    }
}
