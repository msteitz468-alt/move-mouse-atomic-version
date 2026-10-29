using System;

namespace ellabi.Platform
{
    /// <summary>
    /// Separates the user's genuine input from input we injected ourselves, for backends
    /// whose idle timer is reset by synthetic events too (X11: xprintidle counts XTEST
    /// input from xdotool). Only the time of the most recent input is observable, so a
    /// reading that falls inside one of our injection windows is attributed to us, and
    /// idle time keeps counting from the last input we know was the user's.
    ///
    /// Trade-off: user input made during an injection is indistinguishable from ours and
    /// is missed, but any input after it is picked up on the next reading.
    /// </summary>
    public sealed class InjectedInputFilter
    {
        // Slack for process start-up/exit and clock differences between us and the X server.
        private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(250);

        private readonly object _lock = new();
        private int _inFlight;
        private DateTime _injectionStartUtc = DateTime.MinValue;
        private DateTime _injectionEndUtc = DateTime.MinValue;
        private DateTime _lastUserInputUtc;

        /// <param name="nowUtc">Treated as the user's last input, since they just launched us.</param>
        public InjectedInputFilter(DateTime nowUtc)
        {
            _lastUserInputUtc = nowUtc;
        }

        public void BeginInjection(DateTime nowUtc)
        {
            lock (_lock)
            {
                // Overlapping injections share one window, opened by the first.
                if (_inFlight++ == 0) _injectionStartUtc = nowUtc;
            }
        }

        public void EndInjection(DateTime nowUtc)
        {
            lock (_lock)
            {
                if (_inFlight > 0) _inFlight--;
                _injectionEndUtc = nowUtc;
            }
        }

        /// <summary>
        /// Convert a raw idle reading taken at <paramref name="nowUtc"/> into time since the
        /// user's last genuine input.
        /// </summary>
        public TimeSpan Filter(TimeSpan rawIdle, DateTime nowUtc)
        {
            var lastInputUtc = nowUtc - rawIdle;

            lock (_lock)
            {
                var ours = lastInputUtc + Tolerance >= _injectionStartUtc &&
                           (_inFlight > 0 || lastInputUtc - Tolerance <= _injectionEndUtc);

                if (!ours && lastInputUtc > _lastUserInputUtc)
                    _lastUserInputUtc = lastInputUtc;

                var idle = nowUtc - _lastUserInputUtc;
                return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
            }
        }
    }
}
