using System;
using System.IO;

namespace NavisworksIfcExporter.Core
{
    internal static class PluginLogger
    {
        private static readonly string _logPath;
        private static readonly object _lock = new object();

        static PluginLogger()
        {
            const string devRoot = @"c:\dev\navisworks-ifc-exporter";
            string dir = Directory.Exists(devRoot)
                ? Path.Combine(devRoot, "logs")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Navisworks 2026", "Plugins", "NavisworksIfcExporter", "logs");

            try { Directory.CreateDirectory(dir); } catch { }
            _logPath = Path.Combine(dir, "plugin.log");
        }

        public static string LogPath => _logPath;

        public static void Info(string msg)  => Write("INFO ", msg);
        public static void Warn(string msg)  => Write("WARN ", msg);
        public static void Error(string msg, Exception? ex = null)
        {
            string full = ex == null ? msg
                : $"{msg} | {ex.GetType().Name}: {ex.Message}\n    {ex.StackTrace?.Replace("\n", "\n    ")}";
            Write("ERROR", full);
        }

        public static void Clear()
        {
            try { lock (_lock) File.Delete(_logPath); } catch { }
        }

        private static void Write(string level, string msg)
        {
            try
            {
                lock (_lock)
                    File.AppendAllText(_logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}{Environment.NewLine}");
            }
            catch { /* never crash the plugin over logging */ }
        }
    }
}
