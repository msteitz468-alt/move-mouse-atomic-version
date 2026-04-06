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
                if (BreakOnUserActivity && _previousLocations.Count > 0 && !_previousLocations.Contains(GetCursorPosition()))
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
    }
}
