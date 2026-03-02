using System;
using System.IO;
using System.Windows.Forms;

namespace Yaml_Creator
{
    public static class CrossPlatformFileLoader
    {
        public const string RootFolderName = "YARChipelago";
        public static bool IsRunningUnderProton()
        {
            return string.Equals(Environment.UserName, "steamuser", StringComparison.OrdinalIgnoreCase);
        }

        public static string TryGetExistingRoot(bool preferLinux = true)
        {
            if (preferLinux)
            {
                var linux = TryGetExistingLinuxRoot();
                if (linux != null) return linux;

                var win = TryGetExistingWindowsRoot();
                if (win != null) return win;

                return null;
            }
            else
            {
                var win = TryGetExistingWindowsRoot();
                if (win != null) return win;

                var linux = TryGetExistingLinuxRoot();
                if (linux != null) return linux;

                return null;
            }
        }

        public static string TryGetSeedsFolder(bool preferLinux = true)
        {
            return TryGetExistingSubfolder("seeds", preferLinux);
        }

        public static string TryGetConnectionJson(bool preferLinux = true)
        {
            return TryGetExistingFile("connection.json", preferLinux);
        }

        public static string TryGetSongExportJson(bool preferLinux = true)
        {
            return TryGetExistingFile("SongExport.json", preferLinux);
        }

        public static string TryGetExistingFile(string fileName, bool preferLinux = true)
        {
            var root = TryGetExistingRoot(preferLinux);
            if (root == null) return null;

            var path = Path.Combine(root, fileName);
            return File.Exists(path) ? path : null;
        }

        public static string TryGetExistingSubfolder(string folderName, bool preferLinux = true)
        {
            var root = TryGetExistingRoot(preferLinux);
            if (root == null) return null;

            var path = Path.Combine(root, folderName);
            return Directory.Exists(path) ? path : null;
        }

        private static string TryGetExistingLinuxRoot()
        {
            var path = GetLinuxRootCandidateOrNull();
            if (string.IsNullOrEmpty(path)) return null;
            return Directory.Exists(path) ? path : null;
        }

        private static string GetLinuxRootCandidateOrNull()
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                var zHome = @"Z:" + home.Replace("/", "\\");
                var p = Path.Combine(zHome, ".config", RootFolderName);
                return p;
            }
            if (Directory.Exists(@"Z:\home"))
            {
                var user = Environment.UserName;
                if (!string.IsNullOrEmpty(user))
                {
                    var p = Path.Combine(@"Z:\home", user, ".config", RootFolderName);
                    return p;
                }
            }

            return null;
        }

        private static string TryGetExistingWindowsRoot()
        {
            var path = GetWindowsRootCandidateOrNull();
            if (string.IsNullOrEmpty(path)) return null;
            return Directory.Exists(path) ? path : null;
        }

        private static string GetWindowsRootCandidateOrNull()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return null;
            return Path.Combine(appData, RootFolderName);
        }
    }
}