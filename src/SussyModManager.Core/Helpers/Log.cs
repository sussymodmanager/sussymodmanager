using System;
using System.IO;
using System.Threading;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Tiny append-only logger writing to %DataRoot%/logs/app.log with simple size-based rotation.
    /// Never throws - logging must not crash the app.
    /// </summary>
    public static class Log
    {
        private const long MaxBytes = 1_000_000; // ~1 MB before rotating to app.log.1
        private static readonly object Gate = new object();

        private static string LogDir
        {
            get
            {
                var dir = Path.Combine(PlatformInfo.DataRoot, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogFile => Path.Combine(LogDir, "app.log");

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message, Exception ex = null) => Write("ERROR", message, ex);

        public static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (Gate)
                {
                    Rotate();
                    var line =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] [t{Thread.CurrentThread.ManagedThreadId}] {message}";
                    if (ex != null)
                        line += Environment.NewLine + ex;
                    File.AppendAllText(LogFile, line + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private static void Rotate()
        {
            try
            {
                var file = LogFile;
                if (!File.Exists(file))
                    return;
                if (new FileInfo(file).Length < MaxBytes)
                    return;
                var backup = file + ".1";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(file, backup);
            }
            catch
            {
            }
        }
    }
}
