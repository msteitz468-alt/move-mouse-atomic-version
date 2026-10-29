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
    ///
    /// Devices are not opened just once: a background supervisor rescans /dev/input on a
    /// short interval and (re)opens any device we are not currently reading. This keeps
    /// activity detection alive across USB hotplug, wireless-receiver power-save, and
    /// suspend/resume — all of which tear down and re-enumerate the event* nodes. Without
    /// it, a re-enumeration would silently strand the monitor on dead file handles, idle
    /// time would freeze, and auto-pause would stop firing while the cursor kept moving.
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

        // How often the supervisor looks for devices that appeared or re-enumerated.
        private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(3);

        private long _lastActivityTicks;
        private volatile bool _running;
        private readonly bool _excludeVirtualDevice;

        private readonly object _lock = new();
        // Device paths we currently have a live reader thread for.
        private readonly HashSet<string> _openPaths = new();
        private readonly List<FileStream> _streams = new();
        private Thread? _supervisor;

        /// <summary>True if at least one input device could be opened for reading.</summary>
        public bool IsAvailable { get; private set; }

        public EvdevActivityMonitor(bool excludeVirtualDevice = true)
        {
            _excludeVirtualDevice = excludeVirtualDevice;
            _lastActivityTicks = DateTime.UtcNow.Ticks;
            _running = true;

            // Open whatever is present now, then keep watching for devices that appear or
            // re-appear later.
            Scan();

            _supervisor = new Thread(SupervisorLoop)
            {
                IsBackground = true,
                Name = "evdev-supervisor"
            };
            _supervisor.Start();

            int count;
            lock (_lock) { count = _streams.Count; }
            StaticCode.Logger?.Here().Information(
                "evdev activity monitor: {Count} device(s) open at start, available={Avail}",
                count, IsAvailable);
        }

        /// <summary>Time since the last genuine keyboard or mouse event.</summary>
        public TimeSpan GetIdleTime()
        {
            var last = new DateTime(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);
            var idle = DateTime.UtcNow - last;
            return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
        }

        private void SupervisorLoop()
        {
            while (_running)
            {
                Thread.Sleep(RescanInterval);
                if (!_running) break;
                try { Scan(); }
                catch (Exception ex)
                {
                    StaticCode.Logger?.Here().Warning("evdev: rescan failed ({Msg})", ex.Message);
                }
            }
        }

        /// <summary>
        /// Open a reader thread for every input device we are not already reading. Safe to
        /// call repeatedly: devices already in <see cref="_openPaths"/> are skipped, so a
        /// node only gets reopened once its previous reader has exited (and removed itself).
        /// </summary>
        private void Scan()
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
                lock (_lock)
                {
                    if (_openPaths.Contains(path)) continue;
                }

                var name = ReadDeviceName(path);
                if (_excludeVirtualDevice && name != null &&
                    (name.Contains("ydotool", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("MoveMouse", StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // our own injection / scroll devices
                }

                FileStream fs;
                try
                {
                    fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, bufferSize: 0, useAsync: false);
                }
                catch
                {
                    // Unreadable device (e.g. permissions) or it vanished between the
                    // directory listing and the open — skip; a later rescan may catch it.
                    continue;
                }

                lock (_lock)
                {
                    _openPaths.Add(path);
                    _streams.Add(fs);
                    IsAvailable = true;
                }

                var thread = new Thread(() => ReadLoop(fs, path))
                {
                    IsBackground = true,
                    Name = "evdev-monitor"
                };
                thread.Start();
            }
        }

        private void ReadLoop(FileStream fs, string path)
        {
            var buf = new byte[EventSize * 32];
            try
            {
                while (_running)
                {
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n <= 0) break; // device removed / re-enumerated — supervisor reopens it

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
                // Device removed or closed (unplug, re-enumeration, shutdown) — let the
                // thread end and clean up below; the supervisor revives the node if it
                // reappears.
            }
            finally
            {
                lock (_lock)
                {
                    _openPaths.Remove(path);
                    _streams.Remove(fs);
                }
                try { fs.Dispose(); } catch { /* ignore */ }
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
            lock (_lock)
            {
                foreach (var fs in _streams)
                {
                    try { fs.Dispose(); } catch { /* ignore */ }
                }
                _streams.Clear();
                _openPaths.Clear();
            }
        }
    }
}
