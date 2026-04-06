using ellabi.Platform;
using ellabi.Schedules;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace ellabi
{
    public static class StaticCode
    {
        public delegate void ScheduleArrivedHandler(ScheduleBase.ScheduleAction action);
        public delegate void UpdateAvailablityChangedHandler(bool updateAvailable);
        public delegate void RefreshSchedulesHandler();

        public static event ScheduleArrivedHandler? ScheduleArrived;
        public static event UpdateAvailablityChangedHandler? UpdateAvailablityChanged;
        public static event RefreshSchedulesHandler? RefreshSchedules;

        public const string PayPalUrl = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=QZTWHD9CRW5XN";
        public const string HomePageUrl = "http://www.movemouse.co.uk";
        public const string HelpPageUrl = "https://github.com/sw3103/movemouse/wiki";
        public const string GitHubUrl = "https://github.com/sw3103/movemouse";
        public const string CronHelpUrl = "http://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html";
        public const string UpdateXmlUrl = "https://raw.githubusercontent.com/sw3103/movemouse/master/Update_4x.xml";
        public const string MailAddress = "contact@movemouse.co.uk";
        public const string RunRegistryValueName = "Move Mouse";

        // Linux XDG-compliant paths
        public static string WorkingDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            "ellanet", "move-mouse");

        public static string TempDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ??
            Path.Combine(Path.GetTempPath(), "ellanet"),
            "move-mouse");

        public static string UpdateUrl = string.Empty;
        public static ILogger? Logger;
        public static IInputProvider? InputProvider;
        public static Lazy<Dictionary<int, KeyValuePair<string, string>>> VirtualKeys =
            new Lazy<Dictionary<int, KeyValuePair<string, string>>>(GetVirtualKeys);

        private static string _logPath = string.Empty;
        private static LoggingLevelSwitch _loggingLevelSwitch = new LoggingLevelSwitch();

        public static string SettingsXmlPath => Path.Combine(WorkingDirectory, "Settings.xml");
        public static string AutostartDesktopPath => Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            "autostart", "move-mouse.desktop");

        public static string LogPath => _logPath;

        private static Dictionary<int, KeyValuePair<string, string>> GetVirtualKeys()
        {
            return new Dictionary<int, KeyValuePair<string, string>>
            {
                [0x08] = new("BACK",       "Backspace key"),
                [0x09] = new("TAB",        "Tab key"),
                [0x0D] = new("RETURN",     "Enter key"),
                [0x10] = new("SHIFT",      "Shift key"),
                [0x11] = new("CONTROL",    "Ctrl key"),
                [0x12] = new("MENU",       "Alt key"),
                [0x13] = new("PAUSE",      "Pause key"),
                [0x14] = new("CAPITAL",    "Caps lock key"),
                [0x1B] = new("ESCAPE",     "Esc key"),
                [0x20] = new("SPACE",      "Spacebar key"),
                [0x21] = new("PRIOR",      "Page up key"),
                [0x22] = new("NEXT",       "Page down key"),
                [0x23] = new("END",        "End key"),
                [0x24] = new("HOME",       "Home key"),
                [0x25] = new("LEFT",       "Left arrow key"),
                [0x26] = new("UP",         "Up arrow key"),
                [0x27] = new("RIGHT",      "Right arrow key"),
                [0x28] = new("DOWN",       "Down arrow key"),
                [0x2C] = new("SNAPSHOT",   "Print screen key"),
                [0x2D] = new("INSERT",     "Insert key"),
                [0x2E] = new("DELETE",     "Delete key"),
                [0x30] = new("0", "0 key"), [0x31] = new("1", "1 key"),
                [0x32] = new("2", "2 key"), [0x33] = new("3", "3 key"),
                [0x34] = new("4", "4 key"), [0x35] = new("5", "5 key"),
                [0x36] = new("6", "6 key"), [0x37] = new("7", "7 key"),
                [0x38] = new("8", "8 key"), [0x39] = new("9", "9 key"),
                [0x41] = new("A", "A key"), [0x42] = new("B", "B key"),
                [0x43] = new("C", "C key"), [0x44] = new("D", "D key"),
                [0x45] = new("E", "E key"), [0x46] = new("F", "F key"),
                [0x47] = new("G", "G key"), [0x48] = new("H", "H key"),
                [0x49] = new("I", "I key"), [0x4A] = new("J", "J key"),
                [0x4B] = new("K", "K key"), [0x4C] = new("L", "L key"),
                [0x4D] = new("M", "M key"), [0x4E] = new("N", "N key"),
                [0x4F] = new("O", "O key"), [0x50] = new("P", "P key"),
                [0x51] = new("Q", "Q key"), [0x52] = new("R", "R key"),
                [0x53] = new("S", "S key"), [0x54] = new("T", "T key"),
                [0x55] = new("U", "U key"), [0x56] = new("V", "V key"),
                [0x57] = new("W", "W key"), [0x58] = new("X", "X key"),
                [0x59] = new("Y", "Y key"), [0x5A] = new("Z", "Z key"),
                [0x5B] = new("LWIN",       "Left Super key"),
                [0x60] = new("NUMPAD0", "Numpad 0"), [0x61] = new("NUMPAD1", "Numpad 1"),
                [0x62] = new("NUMPAD2", "Numpad 2"), [0x63] = new("NUMPAD3", "Numpad 3"),
                [0x64] = new("NUMPAD4", "Numpad 4"), [0x65] = new("NUMPAD5", "Numpad 5"),
                [0x66] = new("NUMPAD6", "Numpad 6"), [0x67] = new("NUMPAD7", "Numpad 7"),
                [0x68] = new("NUMPAD8", "Numpad 8"), [0x69] = new("NUMPAD9", "Numpad 9"),
                [0x70] = new("F1", "F1 key"),  [0x71] = new("F2",  "F2 key"),
                [0x72] = new("F3", "F3 key"),  [0x73] = new("F4",  "F4 key"),
                [0x74] = new("F5", "F5 key"),  [0x75] = new("F6",  "F6 key"),
                [0x76] = new("F7", "F7 key"),  [0x77] = new("F8",  "F8 key"),
                [0x78] = new("F9", "F9 key"),  [0x79] = new("F10", "F10 key"),
                [0x7A] = new("F11","F11 key"), [0x7B] = new("F12", "F12 key"),
                [0x90] = new("NUMLOCK",    "Num lock key"),
                [0x91] = new("SCROLL",     "Scroll lock key"),
                [0xA0] = new("LSHIFT",     "Left Shift key"),
                [0xA1] = new("RSHIFT",     "Right Shift key"),
                [0xA2] = new("LCONTROL",   "Left Ctrl key"),
                [0xA3] = new("RCONTROL",   "Right Ctrl key"),
                [0xA4] = new("LMENU",      "Left Alt key"),
                [0xA5] = new("RMENU",      "Right Alt key"),
                [0xAD] = new("VOLUME_MUTE","Volume Mute key"),
                [0xAE] = new("VOLUME_DOWN","Volume Down key"),
                [0xAF] = new("VOLUME_UP",  "Volume Up key"),
                [0xB0] = new("MEDIA_NEXT_TRACK", "Next Track key"),
                [0xB1] = new("MEDIA_PREV_TRACK", "Prev Track key"),
                [0xB2] = new("MEDIA_STOP",       "Stop Media key"),
                [0xB3] = new("MEDIA_PLAY_PAUSE",  "Play/Pause key"),
            };
        }

        public static void CreateLog()
        {
            try
            {
                _loggingLevelSwitch.MinimumLevel = (LogEventLevel)((int)LogEventLevel.Fatal + 1);
                _logPath = Path.Combine(TempDirectory, "Move Mouse.log");

                if (File.Exists(_logPath))
                {
                    try { File.Delete(_logPath); } catch { }
                }

                Logger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(_loggingLevelSwitch)
                    .WriteTo.File(_logPath,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t[{Level:u3}]\t{MemberName}\t{Message}{NewLine}{Exception}")
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Logger?.Here().Error(ex.Message);
            }
        }

        public static void EnableLog(LogEventLevel minimumLevel)
        {
            try
            {
                _loggingLevelSwitch.MinimumLevel = minimumLevel;
                Logger?.Here().Debug(LogPath);
            }
            catch (Exception ex)
            {
                Logger?.Here().Error(ex.Message);
            }
        }

        public static void DisableLog()
        {
            Logger?.Here().Debug(string.Empty);
            try
            {
                _loggingLevelSwitch.MinimumLevel = (LogEventLevel)((int)LogEventLevel.Fatal + 1);
            }
            catch (Exception ex)
            {
                Logger?.Here().Error(ex.Message);
            }
        }

        public static TimeSpan GetLastInputTime()
        {
            try
            {
                return InputProvider?.GetIdleTime() ?? TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Logger?.Here().Error(ex.Message);
                return TimeSpan.Zero;
            }
        }

        public static void SetLaunchAtLogon(bool enable, string executablePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(AutostartDesktopPath)!;
                Directory.CreateDirectory(dir);

                if (enable)
                {
                    var desktop = $"""
                        [Desktop Entry]
                        Type=Application
                        Name=Move Mouse
                        Comment=Automate mouse movement and keyboard actions
                        Exec={executablePath}
                        Icon=input-mouse
                        X-GNOME-Autostart-enabled=true
                        """;
                    File.WriteAllText(AutostartDesktopPath, desktop);
                }
                else if (File.Exists(AutostartDesktopPath))
                {
                    File.Delete(AutostartDesktopPath);
                }
            }
            catch (Exception ex)
            {
                Logger?.Here().Error(ex.Message);
            }
        }

        public static bool GetLaunchAtLogon()
        {
            return File.Exists(AutostartDesktopPath);
        }

        public static void OnScheduleArrived(ScheduleBase.ScheduleAction action)
        {
            Logger?.Here().Debug(action.ToString());
            ScheduleArrived?.Invoke(action);
        }

        public static void OnUpdateAvailablityChanged(bool updateAvailable)
        {
            UpdateAvailablityChanged?.Invoke(updateAvailable);
        }

        public static void OnRefreshSchedules()
        {
            RefreshSchedules?.Invoke();
        }
    }

    public static class LoggerExtensions
    {
        public static ILogger Here(this ILogger logger,
            [CallerMemberName] string memberName = "",
            [CallerFilePath]   string sourceFilePath = "",
            [CallerLineNumber] int    sourceLineNumber = 0)
        {
            return logger
                .ForContext("MemberName",   memberName)
                .ForContext("FilePath",     sourceFilePath)
                .ForContext("LineNumber",   sourceLineNumber);
        }
    }
}
