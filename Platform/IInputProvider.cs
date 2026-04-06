using System;
using System.Drawing;

namespace ellabi.Platform
{
    public enum LinuxMouseButton { Left = 1, Middle = 2, Right = 3 }
    public enum LinuxScrollDirection { Up, Down, Left, Right }

    public interface IInputProvider
    {
        /// <summary>Move cursor by (dx, dy) pixels relative to current position.</summary>
        void MoveRelative(int dx, int dy);

        /// <summary>Move cursor to absolute screen coordinates.</summary>
        void MoveTo(int x, int y);

        /// <summary>Get current cursor position.</summary>
        Point GetPosition();

        /// <summary>Click a mouse button (down + up).</summary>
        void Click(LinuxMouseButton button);

        /// <summary>Press a mouse button down (hold).</summary>
        void MouseDown(LinuxMouseButton button);

        /// <summary>Release a mouse button.</summary>
        void MouseUp(LinuxMouseButton button);

        /// <summary>Scroll the mouse wheel. amount is in wheel clicks (120 = one notch on Windows).</summary>
        void Scroll(LinuxScrollDirection direction, uint amount);

        /// <summary>Send a key press using an xdotool-style key name (e.g. "Return", "ctrl+c").</summary>
        void KeyPress(string keyName);

        /// <summary>Get time since last user input event.</summary>
        TimeSpan GetIdleTime();

        /// <summary>Activate/raise a window by its title.</summary>
        void ActivateWindow(string windowTitle);

        /// <summary>Map a Windows Virtual Key code to a platform key name.</summary>
        string VkToKeyName(int vkCode);
    }
}
