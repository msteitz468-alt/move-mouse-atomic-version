using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace ellabi.Platform
{
    /// <summary>
    /// Input provider backed by xdotool (X11) and xprintidle.
    /// Requires: apt install xdotool xprintidle wmctrl
    /// </summary>
    public class XdotoolInputProvider : IInputProvider
    {
        // Windows Virtual Key code -> xdotool keysym name
        private static readonly Dictionary<int, string> VkMap = new()
        {
            [0x08] = "BackSpace",
            [0x09] = "Tab",
            [0x0C] = "Clear",
            [0x0D] = "Return",
            [0x10] = "shift",
            [0x11] = "ctrl",
            [0x12] = "alt",
            [0x13] = "Pause",
            [0x14] = "Caps_Lock",
            [0x1B] = "Escape",
            [0x20] = "space",
            [0x21] = "Prior",
            [0x22] = "Next",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2C] = "Print",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x30] = "0", [0x31] = "1", [0x32] = "2", [0x33] = "3", [0x34] = "4",
            [0x35] = "5", [0x36] = "6", [0x37] = "7", [0x38] = "8", [0x39] = "9",
            [0x41] = "a", [0x42] = "b", [0x43] = "c", [0x44] = "d", [0x45] = "e",
            [0x46] = "f", [0x47] = "g", [0x48] = "h", [0x49] = "i", [0x4A] = "j",
            [0x4B] = "k", [0x4C] = "l", [0x4D] = "m", [0x4E] = "n", [0x4F] = "o",
            [0x50] = "p", [0x51] = "q", [0x52] = "r", [0x53] = "s", [0x54] = "t",
            [0x55] = "u", [0x56] = "v", [0x57] = "w", [0x58] = "x", [0x59] = "y",
            [0x5A] = "z",
            [0x5B] = "super",
            [0x60] = "KP_0", [0x61] = "KP_1", [0x62] = "KP_2", [0x63] = "KP_3",
            [0x64] = "KP_4", [0x65] = "KP_5", [0x66] = "KP_6", [0x67] = "KP_7",
            [0x68] = "KP_8", [0x69] = "KP_9",
            [0x6A] = "KP_Multiply",
            [0x6B] = "KP_Add",
            [0x6D] = "KP_Subtract",
            [0x6E] = "KP_Decimal",
            [0x6F] = "KP_Divide",
            [0x70] = "F1",  [0x71] = "F2",  [0x72] = "F3",  [0x73] = "F4",
            [0x74] = "F5",  [0x75] = "F6",  [0x76] = "F7",  [0x77] = "F8",
            [0x78] = "F9",  [0x79] = "F10", [0x7A] = "F11", [0x7B] = "F12",
            [0x90] = "Num_Lock",
            [0x91] = "Scroll_Lock",
            [0xA0] = "shift", [0xA1] = "shift",
            [0xA2] = "ctrl",  [0xA3] = "ctrl",
            [0xA4] = "alt",   [0xA5] = "alt",
            [0xAD] = "XF86AudioMute",
            [0xAE] = "XF86AudioLowerVolume",
            [0xAF] = "XF86AudioRaiseVolume",
            [0xB0] = "XF86AudioNext",
            [0xB1] = "XF86AudioPrev",
            [0xB2] = "XF86AudioStop",
            [0xB3] = "XF86AudioPlay",
        };

        // X11 can read the pointer and xprintidle reports idle time.
        public bool SupportsPositionQuery => true;
        public bool SupportsIdleQuery => true;

        public void MoveRelative(int dx, int dy)
        {
            Run("xdotool", $"mousemove_relative --sync -- {dx} {dy}");
        }

        public void MoveTo(int x, int y)
        {
            Run("xdotool", $"mousemove --sync {x} {y}");
        }

        public Point GetPosition()
        {
            var output = RunWithOutput("xdotool", "getmouselocation --shell");
            // Output: X=123\nY=456\n...
            var x = ParseShellVar(output, "X");
            var y = ParseShellVar(output, "Y");
            return new Point(x, y);
        }

        public void Click(LinuxMouseButton button)
        {
            Run("xdotool", $"click {(int)button}");
        }

        public void MouseDown(LinuxMouseButton button)
        {
            Run("xdotool", $"mousedown {(int)button}");
        }

        public void MouseUp(LinuxMouseButton button)
        {
            Run("xdotool", $"mouseup {(int)button}");
        }

        public void Scroll(LinuxScrollDirection direction, uint amount)
        {
            // xdotool click 4=up, 5=down, 6=left, 7=right (each click = one notch)
            int button = direction switch
            {
                LinuxScrollDirection.Up    => 4,
                LinuxScrollDirection.Down  => 5,
                LinuxScrollDirection.Left  => 6,
                LinuxScrollDirection.Right => 7,
                _ => 5
            };
            // amount / 120 = Windows wheel notches; clamp to at least 1
            uint notches = Math.Max(1, amount / 120);
            for (uint i = 0; i < notches; i++)
                Run("xdotool", $"click {button}");
        }

        public void KeyPress(string keyName)
        {
            Run("xdotool", $"key {keyName}");
        }

        public TimeSpan GetIdleTime()
        {
            try
            {
                var ms = RunWithOutput("xprintidle", "").Trim();
                if (long.TryParse(ms, out var milliseconds))
                    return TimeSpan.FromMilliseconds(milliseconds);
            }
            catch
            {
                // xprintidle may not be installed; return zero
            }
            return TimeSpan.Zero;
        }

        public void ActivateWindow(string windowTitle)
        {
            // Try wmctrl first, fall back to xdotool
            if (Run("wmctrl", $"-a \"{windowTitle}\"") != 0)
                Run("xdotool", $"search --name \"{windowTitle}\" windowactivate");
        }

        public string VkToKeyName(int vkCode)
        {
            return VkMap.TryGetValue(vkCode, out var name) ? name : $"0x{vkCode:X2}";
        }

        private static int Run(string exe, string args)
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
                psi.Environment["DISPLAY"] = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
                using var proc = new Process { StartInfo = psi };
                proc.Start();
                proc.WaitForExit();
                return proc.ExitCode;
            }
            catch
            {
                return -1;
            }
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
                psi.Environment["DISPLAY"] = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
                using var proc = new Process { StartInfo = psi };
                proc.Start();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ParseShellVar(string output, string name)
        {
            var match = Regex.Match(output, $@"^{name}=(-?\d+)", RegexOptions.Multiline);
            return match.Success && int.TryParse(match.Groups[1].Value, out var v) ? v : 0;
        }
    }
}
