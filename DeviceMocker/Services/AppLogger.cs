using System;
using System.IO;
using DeviceMocker.Core;

namespace DeviceMocker.Services
{
    internal static class AppLogger
    {
        private const long MaxLogBytes = 512 * 1024;
        private static readonly object SyncRoot = new();

        public static string LogFile => Path.Combine(AppConstants.LogsFolder, "DeviceMocker.log");

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message, Exception exception) =>
            Write("ERROR", $"{message}{Environment.NewLine}{exception}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(AppConstants.LogsFolder);
                    RotateIfNeeded();
                    File.AppendAllText(
                        LogFile,
                        $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never prevent the app from starting or closing.
            }
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(LogFile) || new FileInfo(LogFile).Length < MaxLogBytes)
                return;

            var previousLog = Path.Combine(AppConstants.LogsFolder, "DeviceMocker.previous.log");
            File.Move(LogFile, previousLog, true);
        }
    }
}
