using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ellabi.Platform
{
    /// <summary>
    /// Detects real user input on Linux by reading raw events from the evdev devices
    /// under /dev/input/event*. This works on Wayland (where the compositor exposes no
    /// idle or pointer query to clients) provided the process can read those devices —
    /// i.e. it belongs to the 'input' group.
    ///
    /// The device that ydotool injects through (/dev/uinput, named
    /// "ydotoold virtual device") is excluded, so input the application generates itself
    /// does NOT reset the idle timer — only genuine keyboard/mouse activity does.
    /// </summary>
    public sealed class EvdevActivityMonitor : IDisposable
    {
        // struct input_event on 64-bit Linux: timeval(16) + type(2) + code(2) + value(4).
        private const int EventSize = 24;
        private const int TypeOffset = 16;

        // evdev event types that represent user activity.
        private const ushort EV_KEY = 0x01; // key / button presses
        private const ushort EV_REL = 0x02; // relative motion (mice)
        private const ushort EV_ABS = 0x03; // absolute motion (touchpads, touchscreens)

        private long _lastActivityTicks;
        private volatile bool _running;
        private readonly List<FileStream> _streams = new();

        /// <summary>True if at least one input device could be opened for reading.</summary>
        public bool IsAvailable { get; private set; }

        public EvdevActivityMonitor(bool excludeVirtualDevice = true)
        {
            _lastActivityTicks = DateTime.UtcNow.Ticks;
            _running = true;
            Start(excludeVirtualDevice);
        }

        /// <summary>Time since the last genuine keyboard or mouse event.</summary>
        public TimeSpan GetIdleTime()
        {
            var last = new DateTime(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);
            var idle = DateTime.UtcNow - last;
            return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
        }

        private void Start(bool excludeVirtualDevice)
        {
            string[] devices;
            try
            {
                devices = Directory.GetFiles("/dev/input", "event*");
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Warning("evdev: cannot list /dev/input ({Msg})", ex.Message);
                return;
            }

            foreach (var path in devices)
            {
                var name = ReadDeviceName(path);
                if (excludeVirtualDevice && name != null &&
                    (name.Contains("ydotool", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("MoveMouse", StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // our own injection / scroll devices
                }

                try
                {
                    var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, bufferSize: 0, useAsync: false);
                    _streams.Add(fs);
                    IsAvailable = true;

                    var thread = new Thread(() => ReadLoop(fs, name ?? path))
                    {
                        IsBackground = true,
                        Name = "evdev-monitor"
                    };
                    thread.Start();
                }
                catch
                {
                    // Unreadable device (e.g. permissions) — skip it.
                }
            }

            StaticCode.Logger?.Here().Information(
                "evdev activity monitor: {Count} device(s) open, available={Avail}",
                _streams.Count, IsAvailable);
        }

        private void ReadLoop(FileStream fs, string label)
        {
            var buf = new byte[EventSize * 32];
            try
            {
                while (_running)
                {
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n <= 0) break;

                    for (int off = 0; off + EventSize <= n; off += EventSize)
                    {
                        ushort type = BitConverter.ToUInt16(buf, off + TypeOffset);
                        if (type == EV_KEY || type == EV_REL || type == EV_ABS)
                        {
                            Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
                        }
                    }
                }
            }
            catch
            {
                // Device removed or closed during shutdown — let the thread end quietly.
            }
        }

        private static string? ReadDeviceName(string eventPath)
        {
            try
            {
                var ev = Path.GetFileName(eventPath); // e.g. "event9"
                return File.ReadAllText($"/sys/class/input/{ev}/device/name").Trim();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _running = false;
            foreach (var fs in _streams)
            {
                try { fs.Dispose(); } catch { /* ignore */ }
            }
            _streams.Clear();
        }
    }
}
