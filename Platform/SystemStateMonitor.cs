using System;
using System.Diagnostics;
using System.IO;

namespace ellabi.Platform
{
    /// <summary>
    /// Lightweight, library-free probes for system power and lock state, used to drive
    /// "pause on battery" and "stop when locked". Battery comes from sysfs; lock state
    /// from the freedesktop ScreenSaver D-Bus interface (which KDE implements).
    /// </summary>
    public static class SystemStateMonitor
    {
        /// <summary>
        /// True when running on battery (no mains adapter online). Returns false on
        /// machines with no AC adapter reported (e.g. desktops), so it never pauses there.
        /// </summary>
        public static bool IsOnBattery()
        {
            try
            {
                const string root = "/sys/class/power_supply";
                if (!Directory.Exists(root)) return false;

                bool anyMains = false, anyMainsOnline = false;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    if (ReadTrim(Path.Combine(dir, "type")) != "Mains") continue;
                    anyMains = true;
                    if (ReadTrim(Path.Combine(dir, "online")) == "1") anyMainsOnline = true;
                }

                return anyMains && !anyMainsOnline;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True when the session is locked / screensaver active.</summary>
        public static bool IsScreenLocked()
        {
            // org.freedesktop.ScreenSaver.GetActive -> "(true,)" / "(false,)" on KDE.
            var result = RunWithOutput("gdbus",
                "call --session --dest org.freedesktop.ScreenSaver " +
                "--object-path /org/freedesktop/ScreenSaver " +
                "--method org.freedesktop.ScreenSaver.GetActive");
            return result.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadTrim(string path)
        {
            try { return File.ReadAllText(path).Trim(); }
            catch { return string.Empty; }
        }

        private static string RunWithOutput(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(2000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
