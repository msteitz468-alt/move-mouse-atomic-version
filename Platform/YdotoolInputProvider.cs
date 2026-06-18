using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace ellabi.Platform
{
    /// <summary>
    /// Input provider for Wayland sessions (Plasma/KWin, GNOME, etc.), backed by
    /// ydotool, which injects events through the kernel's /dev/uinput device and so
    /// works regardless of the compositor's restrictions on X11-style automation.
    ///
    /// Requires, at runtime:
    ///   * the `ydotool` package
    ///   * the `ydotoold` daemon running (typically a user systemd service)
    ///   * read/write access to /dev/uinput (udev rule granting the user/group)
    ///
    /// The daemon's socket is taken from $YDOTOOL_SOCKET, falling back to the
    /// documented default $XDG_RUNTIME_DIR/.ydotool_socket.
    ///
    /// Wayland forbids clients from reading the pointer position or querying idle
    /// time, so SupportsPositionQuery and SupportsIdleQuery are both false; callers
    /// must skip the features that depend on them.
    /// </summary>
    public class YdotoolInputProvider : IInputProvider
    {
        // The real pointer still cannot be read on Wayland, but user activity (and hence
        // idle time) is recovered by reading evdev input devices directly.
        public bool SupportsPositionQuery => false;
        public bool SupportsIdleQuery => _activityMonitor.IsAvailable;

        // Reads /dev/input/event* to tell when the user last touched a key or the mouse,
        // excluding ydotool's own injection device. Requires membership of the 'input'
        // group; if it can't open any device, SupportsIdleQuery stays false.
        private readonly EvdevActivityMonitor _activityMonitor = new();

        // A virtual cursor: we cannot read the real pointer on Wayland, so we track
        // where we have moved it to. This keeps GetPosition() self-consistent for any
        // caller that still asks, without claiming to know the true position.
        private int _virtualX;
        private int _virtualY;

        // Windows Virtual Key code -> Linux evdev key code (KEY_* in input-event-codes.h).
        // ydotool's `key` command speaks raw evdev codes, not X keysym names.
        private static readonly Dictionary<int, int> VkMap = new()
        {
            [0x08] = 14,  // BackSpace
            [0x09] = 15,  // Tab
            [0x0D] = 28,  // Return
            [0x10] = 42,  // shift
            [0x11] = 29,  // ctrl
            [0x12] = 56,  // alt
            [0x13] = 119, // Pause
            [0x14] = 58,  // Caps Lock
            [0x1B] = 1,   // Escape
            [0x20] = 57,  // space
            [0x21] = 104, // Page Up
            [0x22] = 109, // Page Down
            [0x23] = 107, // End
            [0x24] = 102, // Home
            [0x25] = 105, // Left
            [0x26] = 103, // Up
            [0x27] = 106, // Right
            [0x28] = 108, // Down
            [0x2C] = 99,  // Print
            [0x2D] = 110, // Insert
            [0x2E] = 111, // Delete
            [0x30] = 11, [0x31] = 2, [0x32] = 3, [0x33] = 4, [0x34] = 5,
            [0x35] = 6,  [0x36] = 7, [0x37] = 8, [0x38] = 9, [0x39] = 10,
            [0x41] = 30, [0x42] = 48, [0x43] = 46, [0x44] = 32, [0x45] = 18,
            [0x46] = 33, [0x47] = 34, [0x48] = 35, [0x49] = 23, [0x4A] = 36,
            [0x4B] = 37, [0x4C] = 38, [0x4D] = 50, [0x4E] = 49, [0x4F] = 24,
            [0x50] = 25, [0x51] = 16, [0x52] = 19, [0x53] = 31, [0x54] = 20,
            [0x55] = 22, [0x56] = 47, [0x57] = 17, [0x58] = 45, [0x59] = 21,
            [0x5A] = 44,
            [0x5B] = 125, // super (left meta)
            [0x60] = 82, [0x61] = 79, [0x62] = 80, [0x63] = 81,
            [0x64] = 75, [0x65] = 76, [0x66] = 77, [0x67] = 71,
            [0x68] = 72, [0x69] = 73,
            [0x6A] = 55, // KP Multiply
            [0x6B] = 78, // KP Add
            [0x6D] = 74, // KP Subtract
            [0x6E] = 83, // KP Decimal
            [0x6F] = 98, // KP Divide
            [0x70] = 59, [0x71] = 60, [0x72] = 61, [0x73] = 62,
            [0x74] = 63, [0x75] = 64, [0x76] = 65, [0x77] = 66,
            [0x78] = 67, [0x79] = 68, [0x7A] = 87, [0x7B] = 88,
            [0x90] = 69, // Num Lock
            [0x91] = 70, // Scroll Lock
            [0xA0] = 42, [0xA1] = 54,  // shift L / R
            [0xA2] = 29, [0xA3] = 97,  // ctrl L / R
            [0xA4] = 56, [0xA5] = 100, // alt L / R
            [0xAD] = 113, // Mute
            [0xAE] = 114, // Volume Down
            [0xAF] = 115, // Volume Up
            [0xB0] = 163, // Next track
            [0xB1] = 165, // Prev track
            [0xB2] = 166, // Stop
            [0xB3] = 164, // Play/Pause
        };

        // ydotool mouse button base codes (see `man ydotool`): LEFT=0x00, RIGHT=0x01,
        // MIDDLE=0x02. Our LinuxMouseButton uses the X convention (Left=1, Right=3).
        private static int ButtonBase(LinuxMouseButton button) => button switch
        {
            LinuxMouseButton.Left   => 0x00,
            LinuxMouseButton.Right  => 0x01,
            LinuxMouseButton.Middle => 0x02,
            _ => 0x00
        };

        public void MoveRelative(int dx, int dy)
        {
            // mousemove without --absolute is relative; `--` guards negative values.
            Run($"mousemove -- {dx} {dy}");
            _virtualX += dx;
            _virtualY += dy;
        }

        public void MoveTo(int x, int y)
        {
            Run($"mousemove --absolute -- {x} {y}");
            _virtualX = x;
            _virtualY = y;
        }

        public Point GetPosition()
        {
            // Wayland cannot report the real pointer; return our tracked virtual cursor.
            // SupportsPositionQuery is false so callers should not rely on this.
            return new Point(_virtualX, _virtualY);
        }

        public void Click(LinuxMouseButton button)
        {
            // 0xC0 = down + up.
            Run($"click 0x{0xC0 | ButtonBase(button):X2}");
        }

        public void MouseDown(LinuxMouseButton button)
        {
            // 0x40 = down only.
            Run($"click 0x{0x40 | ButtonBase(button):X2}");
        }

        public void MouseUp(LinuxMouseButton button)
        {
            // 0x80 = up only.
            Run($"click 0x{0x80 | ButtonBase(button):X2}");
        }

        public void Scroll(LinuxScrollDirection direction, uint amount)
        {
            // ydotool 1.x has no wheel command; mouse-wheel scrolling is not available
            // on this backend. Log once and no-op rather than fail.
            StaticCode.Logger?.Here().Warning(
                "Scroll is not supported by the ydotool (Wayland) backend; ignoring.");
        }

        public void KeyPress(string keyName)
        {
            // keyName is one or more evdev codes (from VkToKeyName) joined with '+',
            // e.g. "29+46" for ctrl+c. Press all down in order, then release in reverse.
            var codes = keyName
                .Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var c) ? c : -1)
                .Where(c => c > 0)
                .ToArray();

            if (codes.Length == 0) return;

            var seq = string.Join(" ", codes.Select(c => $"{c}:1"))
                + " "
                + string.Join(" ", codes.Reverse().Select(c => $"{c}:0"));
            Run($"key {seq}");
        }

        public TimeSpan GetIdleTime()
        {
            // KDE Wayland exposes no idle point-query, so we derive it from raw evdev
            // input instead. Falls back to zero if no input devices could be opened.
            return _activityMonitor.IsAvailable ? _activityMonitor.GetIdleTime() : TimeSpan.Zero;
        }

        public void ActivateWindow(string windowTitle)
        {
            // Raising a window by title is not exposed to clients on Wayland.
            StaticCode.Logger?.Here().Warning(
                "ActivateWindow is not supported on Wayland; ignoring request for \"{Title}\".",
                windowTitle);
        }

        public string VkToKeyName(int vkCode)
        {
            // Return the evdev code as a string; unknown keys yield empty so KeyPress
            // skips them (rather than emitting an invalid token).
            return VkMap.TryGetValue(vkCode, out var code) ? code.ToString() : string.Empty;
        }

        private static int Run(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ydotool",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Point ydotool at the daemon socket if not already set in the env.
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YDOTOOL_SOCKET")))
                {
                    var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                    if (!string.IsNullOrEmpty(runtime))
                        psi.Environment["YDOTOOL_SOCKET"] = $"{runtime}/.ydotool_socket";
                }

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                proc.WaitForExit();
                return proc.ExitCode;
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
                return -1;
            }
        }
    }
}
