using ellabi.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace ellabi.Classes
{
    public class Blackout : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _monday, _tuesday, _wednesday, _thursday, _friday, _saturday, _sunday;
        private TimeSpan _time;
        private TimeSpan _duration;
        private bool _isEnabled;

        public bool IsValid => Monday || Tuesday || Wednesday || Thursday || Friday || Saturday || Sunday;

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public Guid Id { get; set; }

        public bool Monday    { get => _monday;    set { _monday    = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Tuesday   { get => _tuesday;   set { _tuesday   = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Wednesday { get => _wednesday; set { _wednesday = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Thursday  { get => _thursday;  set { _thursday  = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Friday    { get => _friday;    set { _friday    = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Saturday  { get => _saturday;  set { _saturday  = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }
        public bool Sunday    { get => _sunday;    set { _sunday    = value || !IsValid ? true : value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); } }

        [XmlIgnore]
        public TimeSpan Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

        [XmlElement(DataType = "duration", ElementName = "Time")]
        public string TimeString
        {
            get => _time.ToString();
            set { try { _time = string.IsNullOrEmpty(value) ? TimeSpan.Zero : TimeSpan.Parse(value); } catch { } }
        }

        [XmlIgnore]
        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        [XmlElement(DataType = "duration", ElementName = "Duration")]
        public string DurationString
        {
            get => _duration.ToString();
            set { try { _duration = string.IsNullOrEmpty(value) ? TimeSpan.Zero : TimeSpan.Parse(value); } catch { } }
        }

        [XmlIgnore]
        public DayOfWeek[] EnabledDays
        {
            get
            {
                var days = new List<DayOfWeek>();
                if (Monday)    days.Add(DayOfWeek.Monday);
                if (Tuesday)   days.Add(DayOfWeek.Tuesday);
                if (Wednesday) days.Add(DayOfWeek.Wednesday);
                if (Thursday)  days.Add(DayOfWeek.Thursday);
                if (Friday)    days.Add(DayOfWeek.Friday);
                if (Saturday)  days.Add(DayOfWeek.Saturday);
                if (Sunday)    days.Add(DayOfWeek.Sunday);
                return days.ToArray();
            }
        }

        public Blackout()
        {
            Id = Guid.NewGuid();
            Time = DateTime.Now.TimeOfDay.Add(TimeSpan.FromSeconds(-1));
            Duration = TimeSpan.FromHours(1);
            Monday = Tuesday = Wednesday = Thursday = Friday = Saturday = Sunday = true;
            _isEnabled = true;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
            catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
        }
    }
}
