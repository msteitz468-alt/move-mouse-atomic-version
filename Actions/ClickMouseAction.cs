using ellabi.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ellabi.Actions
{
    public class ClickMouseAction : ActionBase
    {
        private MouseButton _button;
        private bool _hold;
        private double _holdInterval;

        public enum MouseButton { Left, Middle, Right }

        public IEnumerable<MouseButton> MouseButtonValues => Enum.GetValues(typeof(MouseButton)).Cast<MouseButton>();

        public override bool IsValid => true;

        public MouseButton Button
        {
            get => _button;
            set { _button = value; OnPropertyChanged(); }
        }

        public bool Hold
        {
            get => _hold;
            set { _hold = value; OnPropertyChanged(); }
        }

        public double HoldInterval
        {
            get => _holdInterval;
            set { _holdInterval = value; OnPropertyChanged(); }
        }

        public ClickMouseAction()
        {
            _button = MouseButton.Left;
            InterruptsIdleTime = true;
        }

        public override bool CanExecute() => IsValid;

        public override void Execute()
        {
            try
            {
                IntervalExecutionCount++;
                StaticCode.Logger?.Here().Information(ToString());

                var platformButton = _button switch
                {
                    MouseButton.Left   => LinuxMouseButton.Left,
                    MouseButton.Middle => LinuxMouseButton.Middle,
                    MouseButton.Right  => LinuxMouseButton.Right,
                    _                  => LinuxMouseButton.Left
                };

                StaticCode.InputProvider?.MouseDown(platformButton);

                if (Hold)
                    Thread.Sleep(Convert.ToInt32(1000 * HoldInterval));

                StaticCode.InputProvider?.MouseUp(platformButton);
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public override string ToString() =>
            $"{GetType().Name} | Name={Name} | Button={Button} | Hold={Hold} | HoldInterval={HoldInterval} | Trigger={Trigger}";
    }
}
