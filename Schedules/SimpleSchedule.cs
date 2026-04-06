using System;
using System.Text;
using System.Xml.Serialization;

namespace ellabi.Schedules
{
    public class SimpleSchedule : ScheduleBase
    {
        private bool _monday, _tuesday, _wednesday, _thursday, _friday, _saturday, _sunday;
        private TimeSpan _time;
        private int _delay;

        public override bool IsValid =>
            (Monday || Tuesday || Wednesday || Thursday || Friday || Saturday || Sunday) &&
            Time < TimeSpan.FromHours(24);

        public override string CronExpression
        {
            get
            {
                var time = Delay == 0 ? Time : Time.Add(TimeSpan.FromSeconds(new Random().Next(0, Delay)));
                if (time.TotalDays >= 1) time = new TimeSpan(23, 59, 59);
                return $"{time.Seconds} {time.Minutes} {time.Hours} ? * {BuildDayPart()}";
            }
        }

        public bool Monday    { get => _monday;    set { _monday    = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Tuesday   { get => _tuesday;   set { _tuesday   = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Wednesday { get => _wednesday; set { _wednesday = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Thursday  { get => _thursday;  set { _thursday  = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Friday    { get => _friday;    set { _friday    = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Saturday  { get => _saturday;  set { _saturday  = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Sunday    { get => _sunday;    set { _sunday    = !value && !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }

        [XmlIgnore]
        public TimeSpan Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        [XmlElement(DataType = "duration", ElementName = "Time")]
        public string TimeString
        {
            get => _time.ToString();
            set => _time = string.IsNullOrEmpty(value) ? TimeSpan.Zero : TimeSpan.Parse(value);
        }

        public int Delay
        {
            get => _delay;
            set { _delay = value; OnPropertyChanged(); }
        }

        public SimpleSchedule()
        {
            Time = DateTime.Now.TimeOfDay.Add(TimeSpan.FromSeconds(-1));
            Monday = Tuesday = Wednesday = Thursday = Friday = Saturday = Sunday = true;
        }

        private string BuildDayPart()
        {
            if (Monday && Tuesday && Wednesday && Thursday && Friday && Saturday && Sunday) return "*";
            var sb = new StringBuilder();
            if (Monday)    sb.Append("MON,");
            if (Tuesday)   sb.Append("TUE,");
            if (Wednesday) sb.Append("WED,");
            if (Thursday)  sb.Append("THU,");
            if (Friday)    sb.Append("FRI,");
            if (Saturday)  sb.Append("SAT,");
            if (Sunday)    sb.Append("SUN,");
            return sb.ToString().TrimEnd(',');
        }
    }
}
