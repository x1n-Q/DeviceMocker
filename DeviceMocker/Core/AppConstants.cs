using System;
using System.IO;

namespace DeviceMocker.Core
{
    public static class AppConstants
    {
        public static readonly string AppDataFolder = ResolveAppDataFolder();

        public static readonly string ProfilesFolder = Path.Combine(AppDataFolder, "Profiles");
        public static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");
        public static readonly string LogsFolder = Path.Combine(AppDataFolder, "Logs");

        public const string AppName = "DeviceMocker";
        public const string AppVersion = "1.1.1";
        public const string AppDescription = "Windows toolkit for simulating device input and emulating POS printer and cash drawer workflows.";
        public const string AppAuthor = "x1n-Q";
        public const string AppGitHub = "https://github.com/x1n-Q/DeviceMocker";

        public const int DefaultCountdownSeconds = 3;
        public const int DefaultDelayPerCharacterMs = 10;
        public const string DefaultSuffix = "Enter";

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(ProfilesFolder);
            Directory.CreateDirectory(LogsFolder);
        }

        private static string ResolveAppDataFolder()
        {
            var configuredPath = Environment.GetEnvironmentVariable("DEVICEMOCKER_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.GetFullPath(configuredPath.Trim());

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeviceMocker");
        }
    }
}
