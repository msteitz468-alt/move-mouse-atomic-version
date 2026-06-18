using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ellabi.Platform
{
    /// <summary>
    /// A virtual mouse-wheel device created through /dev/uinput. ydotool exposes no
    /// scroll command, so we emit REL_WHEEL / REL_HWHEEL events ourselves at the kernel
    /// level, which works on Wayland just like the real wheel.
    ///
    /// Requires write access to /dev/uinput (the 'input' group, same as ydotoold). The
    /// device is named "MoveMouse Virtual Pointer" so the activity monitor can exclude
    /// it — otherwise our own scrolling would register as user activity.
    /// </summary>
    public sealed class UinputScrollDevice : IDisposable
    {
        public const string DeviceName = "MoveMouse Virtual Pointer";

        [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
        [DllImport("libc", SetLastError = true)] private static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] private static extern nint write(int fd, byte[] buf, nint count);
        [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")] private static extern int ioctl(int fd, nuint request, int arg);
        [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")] private static extern int ioctl(int fd, nuint request, byte[] arg);

        private const int O_WRONLY = 0x1, O_NONBLOCK = 0x800;

        // uinput ioctls (_IOW('U', n, ...) / _IO('U', n)); see linux/uinput.h.
        private const nuint UI_SET_EVBIT  = 0x40045564;
        private const nuint UI_SET_KEYBIT = 0x40045565;
        private const nuint UI_SET_RELBIT = 0x40045566;
        private const nuint UI_DEV_SETUP  = 0x405C5503;
        private const nuint UI_DEV_CREATE = 0x5501;
        private const nuint UI_DEV_DESTROY = 0x5502;

        // event types/codes (linux/input-event-codes.h).
        private const ushort EV_SYN = 0, EV_KEY = 1, EV_REL = 2;
        private const ushort REL_WHEEL = 8, REL_HWHEEL = 6, SYN_REPORT = 0;
        private const int BTN_LEFT = 0x110;

        private int _fd = -1;
        private readonly object _lock = new();

        public bool IsAvailable => _fd >= 0;

        public UinputScrollDevice()
        {
            try
            {
                _fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
                if (_fd < 0)
                {
                    StaticCode.Logger?.Here().Warning(
                        "Scroll device: cannot open /dev/uinput (errno {Err}); scrolling disabled.",
                        Marshal.GetLastWin32Error());
                    return;
                }

                // A wheel needs at least one button to be recognised as a pointer.
                ioctl(_fd, UI_SET_EVBIT, EV_KEY);
                ioctl(_fd, UI_SET_KEYBIT, BTN_LEFT);
                ioctl(_fd, UI_SET_EVBIT, EV_REL);
                ioctl(_fd, UI_SET_RELBIT, REL_WHEEL);
                ioctl(_fd, UI_SET_RELBIT, REL_HWHEEL);

                var setup = new byte[92];
                BitConverter.GetBytes((ushort)0x03).CopyTo(setup, 0);   // BUS_USB
                BitConverter.GetBytes((ushort)0x1234).CopyTo(setup, 2); // vendor
                BitConverter.GetBytes((ushort)0x5678).CopyTo(setup, 4); // product
                BitConverter.GetBytes((ushort)1).CopyTo(setup, 6);      // version
                var name = Encoding.ASCII.GetBytes(DeviceName);
                Array.Copy(name, 0, setup, 8, Math.Min(name.Length, 79));

                if (ioctl(_fd, UI_DEV_SETUP, setup) < 0 || ioctl(_fd, UI_DEV_CREATE, 0) < 0)
                {
                    StaticCode.Logger?.Here().Warning("Scroll device: UI_DEV setup/create failed; scrolling disabled.");
                    close(_fd);
                    _fd = -1;
                    return;
                }

                // Give the compositor a moment to enumerate the new device.
                Thread.Sleep(200);
                StaticCode.Logger?.Here().Information("Scroll device created via /dev/uinput.");
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
                _fd = -1;
            }
        }

        /// <summary>Emit <paramref name="notches"/> wheel notches in the given direction.</summary>
        public void Scroll(LinuxScrollDirection direction, int notches)
        {
            if (_fd < 0 || notches <= 0) return;

            var (code, sign) = direction switch
            {
                LinuxScrollDirection.Up    => (REL_WHEEL, 1),
                LinuxScrollDirection.Down  => (REL_WHEEL, -1),
                LinuxScrollDirection.Right => (REL_HWHEEL, 1),
                LinuxScrollDirection.Left  => (REL_HWHEEL, -1),
                _ => (REL_WHEEL, -1)
            };

            lock (_lock)
            {
                for (int i = 0; i < notches; i++)
                {
                    Emit(EV_REL, code, sign);
                    Emit(EV_SYN, SYN_REPORT, 0);
                    Thread.Sleep(10);
                }
            }
        }

        private void Emit(ushort type, ushort code, int value)
        {
            var ev = new byte[24]; // struct input_event: timeval(16) + type + code + value
            BitConverter.GetBytes(type).CopyTo(ev, 16);
            BitConverter.GetBytes(code).CopyTo(ev, 18);
            BitConverter.GetBytes(value).CopyTo(ev, 20);
            write(_fd, ev, 24);
        }

        public void Dispose()
        {
            if (_fd < 0) return;
            try { ioctl(_fd, UI_DEV_DESTROY, 0); close(_fd); } catch { /* ignore */ }
            _fd = -1;
        }
    }
}
