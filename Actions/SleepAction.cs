using System;
using System.Threading;

namespace ellabi.Actions
{
    public class SleepAction : ActionBase
    {
        private double _seconds;
        private double _upperSeconds;
        private bool _random;

        public override bool IsValid => Seconds > 0.0;

        public double Seconds
        {
            get => _seconds;
            set
            {
                if (value > UpperSeconds) UpperSeconds = value;
                _seconds = value < .1 ? .1 : value;
                OnPropertyChanged();
            }
        }

        public double UpperSeconds
        {
            get => _upperSeconds;
            set
            {
                if (value < Seconds) Seconds = value;
                _upperSeconds = value < .1 ? .1 : value;
                OnPropertyChanged();
            }
        }

        public bool Random
        {
            get => _random;
            set { _random = value; OnPropertyChanged(); }
        }

        public SleepAction()
        {
            _seconds = 1;
            _upperSeconds = 2;
        }

        public override bool CanExecute() => IsValid;

        public override void Execute()
        {
            try
            {
                IntervalExecutionCount++;
                StaticCode.Logger?.Here().Information(ToString());
                var sleep = TimeSpan.FromSeconds(
                    Random ? new System.Random().Next((int)Seconds, (int)UpperSeconds) : Seconds);
                Thread.Sleep(sleep);
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public override string ToString() =>
            $"{GetType().Name} | Name={Name} | Random={Random} | Seconds={Seconds} | UpperSeconds={UpperSeconds} | Trigger={Trigger}";
    }
}
