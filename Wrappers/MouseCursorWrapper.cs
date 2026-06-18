using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace ellabi.Wrappers
{
    /// <summary>
    /// Moves the mouse cursor using the platform IInputProvider (xdotool on Linux).
    /// API is kept identical to the Windows version so all Action classes compile unchanged.
    /// </summary>
    public class MouseCursorWrapper
    {
        private readonly HashSet<Point> _previousLocations = new();

        public bool BreakOnUserActivity { get; set; }
        public bool UserActivityDetected { get; private set; }

        public void MoveFromCurrentLocation(Point delta)
        {
            try
            {
                // Break-on-user-activity: stop as soon as the user is at the controls.
                // On X11 we detect this by the pointer being somewhere we didn't put it;
                // on Wayland (no pointer read) we use recent real input from evdev.
                if (BreakOnUserActivity && IsUserActive())
                {
                    UserActivityDetected = true;
                    return;
                }

                StaticCode.InputProvider?.MoveRelative(delta.X, delta.Y);
                _previousLocations.Add(GetCursorPosition());
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void MoveNorth(int distance, int delay)     => MoveAxis(0, -1, distance, delay);
        public void MoveNorthEast(int distance, int delay) => MoveAxis(1, -1, distance, delay);
        public void MoveEast(int distance, int delay)      => MoveAxis(1,  0, distance, delay);
        public void MoveSouthEast(int distance, int delay) => MoveAxis(1,  1, distance, delay);
        public void MoveSouth(int distance, int delay)     => MoveAxis(0,  1, distance, delay);
        public void MoveSouthWest(int distance, int delay) => MoveAxis(-1, 1, distance, delay);
        public void MoveWest(int distance, int delay)      => MoveAxis(-1, 0, distance, delay);
        public void MoveNorthWest(int distance, int delay) => MoveAxis(-1,-1, distance, delay);

        private void MoveAxis(int dx, int dy, int distance, int delay)
        {
            for (int i = 0; i < distance; i++)
            {
                MoveFromCurrentLocation(new Point(dx, dy));
                if (UserActivityDetected) break;
                Thread.Sleep(delay);
            }
        }

        public Point GetCursorPosition()
        {
            try
            {
                return StaticCode.InputProvider?.GetPosition() ?? Point.Empty;
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
                return Point.Empty;
            }
        }

        // Input within this window counts as the user being actively at the machine.
        private static readonly TimeSpan UserActiveWindow = TimeSpan.FromMilliseconds(750);

        /// <summary>
        /// True if the user appears to be operating the machine themselves. Uses the
        /// pointer position on X11; on Wayland uses recent real input (evdev idle time),
        /// which excludes the cursor movement this app generates.
        /// </summary>
        private bool IsUserActive()
        {
            var provider = StaticCode.InputProvider;
            if (provider == null) return false;

            if (provider.SupportsPositionQuery)
                return _previousLocations.Count > 0 && !_previousLocations.Contains(GetCursorPosition());

            if (provider.SupportsIdleQuery)
                return provider.GetIdleTime() < UserActiveWindow;

            return false; // no way to tell — keep moving
        }
    }
}
