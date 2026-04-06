using ellabi.Actions;
using ellabi.Annotations;
using ellabi.Schedules;
using Serilog.Events;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace ellabi.Classes
{
    [XmlInclude(typeof(MoveMouseCursorAction))]
    [XmlInclude(typeof(ClickMouseAction))]
    [XmlInclude(typeof(KeystrokeAction))]
    [XmlInclude(typeof(ScrollMouseAction))]
    [XmlInclude(typeof(SleepAction))]
    [XmlInclude(typeof(PositionMouseCursorAction))]
    [XmlInclude(typeof(CommandAction))]
    [XmlInclude(typeof(ScriptAction))]
    [XmlInclude(typeof(ActivateApplicationAction))]
    [XmlInclude(typeof(SimpleSchedule))]
    [XmlInclude(typeof(AdvancedSchedule))]
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int? _lowerInterval;
        private int? _upperInterval;
        private bool? _randomInterval;
        private bool? _autoPause;
        private bool? _autoResume;
        private int? _autoResumeSeconds;
        private bool? _topmostWhenRunning;
        private bool? _hideFromTaskbar;
        private bool? _hideMainWindow;
        private bool? _hideSystemTrayIcon;
        private bool? _launchAtLogon;
        private bool? _startAtLaunch;
        private bool? _moveMouseHasBeenClicked;
        private ActionBase[]? _actions;
        private ScheduleBase[]? _schedules;
        private Blackout[]? _blackouts;
        private bool? _minimiseOnStop;
        private bool? _enableLogging;
        private bool? _pauseOnBattery;
        private LogEventLevel? _logLevel;
        private bool? _showSystemTrayNotifications;
        private bool? _showTaskbarStatus;

        public int LowerInterval
        {
            get { if (_lowerInterval == null) _lowerInterval = 30; return _lowerInterval.Value; }
            set
            {
                if (value > UpperInterval) UpperInterval = value;
                _lowerInterval = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        public int UpperInterval
        {
            get { if (_upperInterval == null) _upperInterval = 60; return _upperInterval.Value; }
            set
            {
                if (value < LowerInterval) LowerInterval = value;
                _upperInterval = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        public bool RandomInterval
        {
            get { if (_randomInterval == null) _randomInterval = false; return _randomInterval.Value; }
            set { _randomInterval = value; OnPropertyChanged(); }
        }

        public bool AutoPause
        {
            get { if (_autoPause == null) _autoPause = true; return _autoPause.Value; }
            set { _autoPause = value; OnPropertyChanged(); }
        }

        public bool AutoResume
        {
            get { if (_autoResume == null) _autoResume = true; return _autoResume.Value; }
            set { _autoResume = value; OnPropertyChanged(); }
        }

        public int AutoResumeSeconds
        {
            get { if (_autoResumeSeconds == null) _autoResumeSeconds = 60; return _autoResumeSeconds.Value; }
            set { _autoResumeSeconds = value; OnPropertyChanged(); }
        }

        public bool TopmostWhenRunning
        {
            get { if (_topmostWhenRunning == null) _topmostWhenRunning = false; return _topmostWhenRunning.Value; }
            set { _topmostWhenRunning = value; OnPropertyChanged(); }
        }

        public bool HideFromTaskbar
        {
            get { if (_hideFromTaskbar == null) _hideFromTaskbar = false; return _hideFromTaskbar.Value; }
            set { _hideFromTaskbar = value; OnPropertyChanged(); }
        }

        public bool HideMainWindow
        {
            get { if (_hideMainWindow == null) _hideMainWindow = false; return _hideMainWindow.Value; }
            set { _hideMainWindow = value; OnPropertyChanged(); }
        }

        public bool HideSystemTrayIcon
        {
            get { if (_hideSystemTrayIcon == null) _hideSystemTrayIcon = false; return _hideSystemTrayIcon.Value; }
            set { _hideSystemTrayIcon = value; OnPropertyChanged(); }
        }

        public bool LaunchAtLogon
        {
            get { if (_launchAtLogon == null) _launchAtLogon = false; return _launchAtLogon.Value; }
            set { _launchAtLogon = value; OnPropertyChanged(); }
        }

        public bool StartAtLaunch
        {
            get { if (_startAtLaunch == null) _startAtLaunch = false; return _startAtLaunch.Value; }
            set { _startAtLaunch = value; OnPropertyChanged(); }
        }

        public bool MoveMouseHasBeenClicked
        {
            get { if (_moveMouseHasBeenClicked == null) _moveMouseHasBeenClicked = false; return _moveMouseHasBeenClicked.Value; }
            set { _moveMouseHasBeenClicked = value; OnPropertyChanged(); }
        }

        public ActionBase[]? Actions
        {
            get => _actions;
            set { _actions = value; OnPropertyChanged(); }
        }

        public ScheduleBase[]? Schedules
        {
            get => _schedules;
            set { _schedules = value; OnPropertyChanged(); }
        }

        public Blackout[]? Blackouts
        {
            get => _blackouts;
            set { _blackouts = value; OnPropertyChanged(); }
        }

        public bool MinimiseOnStop
        {
            get { if (_minimiseOnStop == null) _minimiseOnStop = false; return _minimiseOnStop.Value; }
            set { _minimiseOnStop = value; OnPropertyChanged(); }
        }

        public bool EnableLogging
        {
            get { if (_enableLogging == null) _enableLogging = false; return _enableLogging.Value; }
            set { _enableLogging = value; OnPropertyChanged(); }
        }

        public bool PauseOnBattery
        {
            get { if (_pauseOnBattery == null) _pauseOnBattery = false; return _pauseOnBattery.Value; }
            set { _pauseOnBattery = value; OnPropertyChanged(); }
        }

        public LogEventLevel LogLevel
        {
            get { if (_logLevel == null) _logLevel = LogEventLevel.Debug; return _logLevel.Value; }
            set { _logLevel = value; OnPropertyChanged(); }
        }

        public bool ShowSystemTrayNotifications
        {
            get { if (_showSystemTrayNotifications == null) _showSystemTrayNotifications = true; return _showSystemTrayNotifications.Value; }
            set { _showSystemTrayNotifications = value; OnPropertyChanged(); }
        }

        public bool ShowTaskbarStatus
        {
            get { if (_showTaskbarStatus == null) _showTaskbarStatus = true; return _showTaskbarStatus.Value; }
            set { _showTaskbarStatus = value; OnPropertyChanged(); }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
            catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
        }
    }
}
