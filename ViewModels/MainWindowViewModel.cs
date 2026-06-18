using Avalonia.Threading;
using ellabi.Actions;
using ellabi.Classes;
using ellabi.Platform;
using ellabi.Jobs;
using ellabi.Schedules;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;
using Timer = System.Timers.Timer;

namespace ellabi.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ── State ──────────────────────────────────────────────────────────
        public enum AppState { Stopped, Running, Paused }

        /// <summary>Richer visual state for the status ring colour.</summary>
        public enum VisualStatus { Idle, Running, Executing, Paused, Sleeping, Battery }

        private AppState _state = AppState.Stopped;
        private string _statusText = "Stopped";
        private bool _updateAvailable;
        private Settings _settings = new();

        // ── Timers ─────────────────────────────────────────────────────────
        private Timer? _intervalTimer;
        private Timer? _countdownTimer;
        private int _countdownSeconds;
        private Timer? _autoPauseCheckTimer;
        private Timer? _environmentTimer;
        private bool _pausedForBattery;
        private IScheduler? _quartzScheduler;

        // ── Commands ───────────────────────────────────────────────────────
        public Utilities.RelayCommand StartCommand { get; }
        public Utilities.RelayCommand StopCommand { get; }
        public Utilities.RelayCommand PauseCommand { get; }
        public Utilities.RelayCommand SaveSettingsCommand { get; }
        public Utilities.RelayCommand AddActionCommand { get; }
        public Utilities.RelayCommand RemoveActionCommand { get; }
        public Utilities.RelayCommand AddScheduleCommand { get; }
        public Utilities.RelayCommand RemoveScheduleCommand { get; }
        public Utilities.RelayCommand AddBlackoutCommand { get; }
        public Utilities.RelayCommand RemoveBlackoutCommand { get; }

        // ── Bound properties ───────────────────────────────────────────────
        public AppState State
        {
            get => _state;
            private set
            {
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsStopped));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(CountdownText));
                StatusText = _state switch
                {
                    AppState.Running => "Running",
                    AppState.Paused  => "Paused",
                    _                => "Stopped"
                };
                RaiseCommandsChanged();
            }
        }

        public bool IsRunning => State == AppState.Running;
        public bool IsStopped => State == AppState.Stopped;
        public bool IsPaused  => State == AppState.Paused;

        private VisualStatus _status = VisualStatus.Idle;
        public VisualStatus Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        /// <summary>Set the visual status from any thread (the ring binding is UI-bound).</summary>
        private void SetStatus(VisualStatus status) => Dispatcher.UIThread.Post(() => Status = status);

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public bool UpdateAvailable
        {
            get => _updateAvailable;
            private set { _updateAvailable = value; OnPropertyChanged(); }
        }

        public string IntervalDescription =>
            Settings.RandomInterval
                ? $"Interval: {Settings.LowerInterval}–{Settings.UpperInterval}s (random)"
                : $"Interval: {Settings.LowerInterval}s";

        public Settings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        // ── Actions editor ─────────────────────────────────────────────────
        private ActionBase? _selectedAction;
        private string _selectedActionTypeName = "Move Cursor";

        /// <summary>Observable view of Settings.Actions so add/remove updates the UI.</summary>
        public ObservableCollection<ActionBase> Actions { get; } = new();

        public ActionBase? SelectedAction
        {
            get => _selectedAction;
            set { _selectedAction = value; OnPropertyChanged(); RaiseCommandsChanged(); }
        }

        public IReadOnlyList<string> ActionTypeNames { get; } = new[]
        {
            "Move Cursor", "Click", "Scroll", "Position Cursor", "Keystroke",
            "Activate Application", "Run Command", "Script", "Sleep"
        };

        public string SelectedActionTypeName
        {
            get => _selectedActionTypeName;
            set { _selectedActionTypeName = value; OnPropertyChanged(); }
        }

        // ── Schedules editor ───────────────────────────────────────────────
        private ScheduleBase? _selectedSchedule;
        private string _selectedScheduleTypeName = "Simple";

        public ObservableCollection<ScheduleBase> Schedules { get; } = new();

        public ScheduleBase? SelectedSchedule
        {
            get => _selectedSchedule;
            set { _selectedSchedule = value; OnPropertyChanged(); RaiseCommandsChanged(); }
        }

        public IReadOnlyList<string> ScheduleTypeNames { get; } = new[] { "Simple", "Advanced" };

        public string SelectedScheduleTypeName
        {
            get => _selectedScheduleTypeName;
            set { _selectedScheduleTypeName = value; OnPropertyChanged(); }
        }

        // ── Blackouts editor ───────────────────────────────────────────────
        private Blackout? _selectedBlackout;

        public ObservableCollection<Blackout> Blackouts { get; } = new();

        public Blackout? SelectedBlackout
        {
            get => _selectedBlackout;
            set { _selectedBlackout = value; OnPropertyChanged(); RaiseCommandsChanged(); }
        }

        public int CountdownSeconds
        {
            get => _countdownSeconds;
            private set { _countdownSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountdownText)); }
        }

        public string CountdownText => IsRunning ? $"Next move in {CountdownSeconds}s" : "";

        // ── Constructor ────────────────────────────────────────────────────
        public MainWindowViewModel()
        {
            StartCommand       = new Utilities.RelayCommand(_ => Start(),  _ => !IsRunning);
            StopCommand        = new Utilities.RelayCommand(_ => Stop(),   _ => !IsStopped);
            PauseCommand       = new Utilities.RelayCommand(_ => Pause(),  _ => IsRunning);
            SaveSettingsCommand = new Utilities.RelayCommand(_ => SaveSettings());
            AddActionCommand    = new Utilities.RelayCommand(_ => AddAction());
            RemoveActionCommand = new Utilities.RelayCommand(_ => RemoveAction(), _ => SelectedAction != null);
            AddScheduleCommand    = new Utilities.RelayCommand(_ => AddSchedule());
            RemoveScheduleCommand = new Utilities.RelayCommand(_ => RemoveSchedule(), _ => SelectedSchedule != null);
            AddBlackoutCommand    = new Utilities.RelayCommand(_ => AddBlackout());
            RemoveBlackoutCommand = new Utilities.RelayCommand(_ => RemoveBlackout(), _ => SelectedBlackout != null);

            LoadSettings();

            if (Settings.Actions == null || Settings.Actions.Length == 0)
            {
                Settings.Actions = new ActionBase[]
                {
                    new MoveMouseCursorAction
                    {
                        Name = "Default Move",
                        Distance = 100,
                        Direction = MoveMouseCursorAction.CursorDirection.Square,
                        Speed = MoveMouseCursorAction.CursorSpeed.Normal,
                        AbortIfUserActivityDetected = true,
                        Repeat = true,
                        IsEnabled = true
                    }
                };
            }

            RebuildActionsCollection();
            RebuildScheduleAndBlackoutCollections();

            StaticCode.ScheduleArrived         += OnScheduleArrived;
            StaticCode.UpdateAvailablityChanged += v => UpdateAvailable = v;
            StaticCode.RefreshSchedules         += RefreshQuartzSchedules;

            InitialiseQuartzAsync().ConfigureAwait(false);

            if (Settings.StartAtLaunch)
                Start();
        }

        // ── Actions add / remove ───────────────────────────────────────────
        private void RebuildActionsCollection()
        {
            Actions.Clear();
            if (Settings.Actions != null)
                foreach (var a in Settings.Actions)
                    Actions.Add(a);
            SelectedAction = Actions.FirstOrDefault();
        }

        private void AddAction()
        {
            var action = CreateAction(SelectedActionTypeName);
            action.Id = Guid.NewGuid();
            Actions.Add(action);
            SyncActionsToSettings();
            SelectedAction = action;
        }

        private void RemoveAction()
        {
            if (SelectedAction == null) return;
            var idx = Actions.IndexOf(SelectedAction);
            Actions.Remove(SelectedAction);
            SyncActionsToSettings();
            SelectedAction = Actions.Count == 0 ? null : Actions[Math.Min(idx, Actions.Count - 1)];
        }

        /// <summary>Mirror the observable collection back into the serialized array
        /// the engine reads from.</summary>
        private void SyncActionsToSettings() => Settings.Actions = Actions.ToArray();

        private static ActionBase CreateAction(string typeName) => typeName switch
        {
            "Click"                => new ClickMouseAction          { Name = "Click",           IsEnabled = true, Repeat = true },
            "Scroll"               => new ScrollMouseAction         { Name = "Scroll",          IsEnabled = true, Repeat = true },
            "Position Cursor"      => new PositionMouseCursorAction { Name = "Position Cursor", IsEnabled = true, Repeat = true },
            "Keystroke"            => new KeystrokeAction           { Name = "Keystroke",       IsEnabled = true, Repeat = true },
            "Activate Application" => new ActivateApplicationAction { Name = "Activate App",    IsEnabled = true, Repeat = true },
            "Run Command"          => new CommandAction             { Name = "Run Command",     IsEnabled = true, Repeat = true },
            "Script"               => new ScriptAction              { Name = "Script",          IsEnabled = true, Repeat = true },
            "Sleep"                => new SleepAction                { Name = "Sleep",           IsEnabled = true, Repeat = true },
            _                      => new MoveMouseCursorAction     { Name = "Move Cursor", Distance = 100, IsEnabled = true, Repeat = true },
        };

        // ── Schedules add / remove ─────────────────────────────────────────
        private void RebuildScheduleAndBlackoutCollections()
        {
            Schedules.Clear();
            if (Settings.Schedules != null)
                foreach (var s in Settings.Schedules) Schedules.Add(s);
            SelectedSchedule = Schedules.FirstOrDefault();

            Blackouts.Clear();
            if (Settings.Blackouts != null)
                foreach (var b in Settings.Blackouts) Blackouts.Add(b);
            SelectedBlackout = Blackouts.FirstOrDefault();
        }

        private void AddSchedule()
        {
            ScheduleBase schedule = SelectedScheduleTypeName == "Advanced"
                ? new AdvancedSchedule { Schedule = "0 0 9 ? * MON-FRI" }
                : new SimpleSchedule();
            Schedules.Add(schedule);
            SyncSchedulesToSettings();
            SelectedSchedule = schedule;
        }

        private void RemoveSchedule()
        {
            if (SelectedSchedule == null) return;
            var idx = Schedules.IndexOf(SelectedSchedule);
            Schedules.Remove(SelectedSchedule);
            SyncSchedulesToSettings();
            SelectedSchedule = Schedules.Count == 0 ? null : Schedules[Math.Min(idx, Schedules.Count - 1)];
        }

        private void SyncSchedulesToSettings()
        {
            Settings.Schedules = Schedules.ToArray();
            RefreshQuartzSchedules();
        }

        // ── Blackouts add / remove ─────────────────────────────────────────
        private void AddBlackout()
        {
            var blackout = new Blackout();
            Blackouts.Add(blackout);
            SyncBlackoutsToSettings();
            SelectedBlackout = blackout;
        }

        private void RemoveBlackout()
        {
            if (SelectedBlackout == null) return;
            var idx = Blackouts.IndexOf(SelectedBlackout);
            Blackouts.Remove(SelectedBlackout);
            SyncBlackoutsToSettings();
            SelectedBlackout = Blackouts.Count == 0 ? null : Blackouts[Math.Min(idx, Blackouts.Count - 1)];
        }

        private void SyncBlackoutsToSettings() => Settings.Blackouts = Blackouts.ToArray();

        // ── Start / Stop / Pause ───────────────────────────────────────────
        public void Start()
        {
            try
            {
                if (State == AppState.Running) return;
                State = AppState.Running;
                SetStatus(VisualStatus.Running);
                StaticCode.Logger?.Here().Information("Start");

                ExecuteActionsForTrigger(ActionBase.EventTrigger.Start);
                ScheduleNextInterval();
                StartAutoPauseCheck();
                StartEnvironmentCheck();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void Stop()
        {
            try
            {
                if (State == AppState.Stopped) return;
                State = AppState.Stopped;
                SetStatus(VisualStatus.Idle);
                StaticCode.Logger?.Here().Information("Stop");

                _intervalTimer?.Stop();
                _countdownTimer?.Stop();
                _autoPauseCheckTimer?.Stop();
                _environmentTimer?.Stop();
                _pausedForBattery = false;
                CountdownSeconds = 0;

                ExecuteActionsForTrigger(ActionBase.EventTrigger.Stop);
                ResetIntervalCounters();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void Pause()
        {
            try
            {
                if (State != AppState.Running) return;
                State = AppState.Paused;
                SetStatus(_pausedForBattery ? VisualStatus.Battery : VisualStatus.Paused);
                StaticCode.Logger?.Here().Information("Pause");
                _intervalTimer?.Stop();
                _countdownTimer?.Stop();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void Resume()
        {
            try
            {
                if (State != AppState.Paused) return;
                State = AppState.Running;
                SetStatus(VisualStatus.Running);
                StaticCode.Logger?.Here().Information("Resume");
                ScheduleNextInterval();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        // ── Interval execution ─────────────────────────────────────────────
        private void ScheduleNextInterval()
        {
            _intervalTimer?.Stop();
            _countdownTimer?.Stop();
            _intervalTimer = new Timer();

            var lower = Math.Max(1, Settings.LowerInterval);
            var upper = Math.Max(lower, Settings.UpperInterval);
            var intervalSeconds = Settings.RandomInterval
                ? new Random().Next(lower, upper)
                : lower;

            CountdownSeconds = intervalSeconds;

            _countdownTimer = new Timer(1000) { AutoReset = true };
            _countdownTimer.Elapsed += (_, _) =>
            {
                if (CountdownSeconds > 0)
                    CountdownSeconds--;
            };
            _countdownTimer.Start();

            _intervalTimer.Interval = intervalSeconds * 1000.0;
            _intervalTimer.AutoReset = false;
            _intervalTimer.Elapsed += OnIntervalElapsed;
            _intervalTimer.Start();
        }

        private void OnIntervalElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (State != AppState.Running) return;

                // Check blackout periods — show the "sleeping" colour while dormant.
                if (IsInBlackout()) { SetStatus(VisualStatus.Sleeping); ScheduleNextInterval(); return; }

                SetStatus(VisualStatus.Executing);
                ExecuteActionsForTrigger(ActionBase.EventTrigger.Interval);

                // Default mouse wiggle if no interval actions are configured
                if (Settings.Actions == null ||
                    !Settings.Actions.Any(a => a.IsEnabled && a.Trigger == ActionBase.EventTrigger.Interval))
                {
                    StaticCode.InputProvider?.MoveRelative(5, 0);
                    StaticCode.InputProvider?.MoveRelative(-5, 0);
                }

                // Brief red flash, then back to the waiting (green) colour.
                Thread.Sleep(200);
                if (State == AppState.Running) SetStatus(VisualStatus.Running);

                ScheduleNextInterval();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void ExecuteActionsForTrigger(ActionBase.EventTrigger trigger)
        {
            try
            {
                if (Settings.Actions == null) return;

                foreach (var action in Settings.Actions.Where(a => a.IsEnabled && a.Trigger == trigger))
                {
                    try
                    {
                        if (action.RepeatMode == ActionBase.IntervalRepeatMode.Throttle &&
                            action.IntervalExecutionCount >= action.IntervalThrottle)
                            continue;

                        if (action.CanExecute())
                        {
                            Task.Run(() =>
                            {
                                try { action.Execute(); }
                                catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        StaticCode.Logger?.Here().Error(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void ResetIntervalCounters()
        {
            if (Settings.Actions == null) return;
            foreach (var action in Settings.Actions)
                action.IntervalExecutionCount = 0;
        }

        // ── Auto-pause ─────────────────────────────────────────────────────
        private void StartAutoPauseCheck()
        {
            _autoPauseCheckTimer?.Stop();
            if (!Settings.AutoPause && !Settings.AutoResume) return;

            _autoPauseCheckTimer = new Timer(2000) { AutoReset = true };
            _autoPauseCheckTimer.Elapsed += OnAutoPauseCheck;
            _autoPauseCheckTimer.Start();
        }

        private void OnAutoPauseCheck(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Auto-pause/resume depend on a real idle-time reading; on backends that
                // cannot provide one (e.g. Wayland) skip rather than act on a zero value.
                if (StaticCode.InputProvider?.SupportsIdleQuery != true)
                    return;

                var idle = StaticCode.GetLastInputTime();

                // This runs on a System.Timers.Timer (background) thread. Pause()/Resume()
                // mutate UI-bound state, which Avalonia only permits on the UI thread, so
                // marshal the calls there to avoid a "Call from invalid thread" exception.
                if (Settings.AutoPause && State == AppState.Running && idle.TotalSeconds < 2)
                {
                    Dispatcher.UIThread.Post(Pause);
                }
                else if (Settings.AutoResume && State == AppState.Paused &&
                         idle.TotalSeconds >= Settings.AutoResumeSeconds)
                {
                    Dispatcher.UIThread.Post(Resume);
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        // ── Power / lock awareness ─────────────────────────────────────────
        private void StartEnvironmentCheck()
        {
            _environmentTimer?.Stop();
            if (!Settings.PauseOnBattery && !Settings.StopWhenLocked) return;

            _environmentTimer = new Timer(4000) { AutoReset = true };
            _environmentTimer.Elapsed += OnEnvironmentCheck;
            _environmentTimer.Start();
        }

        private void OnEnvironmentCheck(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Pause while on battery; resume once mains power returns (only if we
                // were the ones who paused for battery — don't fight a user-activity pause).
                if (Settings.PauseOnBattery)
                {
                    var onBattery = SystemStateMonitor.IsOnBattery();
                    if (onBattery && State == AppState.Running)
                    {
                        _pausedForBattery = true;
                        Dispatcher.UIThread.Post(Pause);
                    }
                    else if (!onBattery && _pausedForBattery && State == AppState.Paused)
                    {
                        _pausedForBattery = false;
                        Dispatcher.UIThread.Post(Resume);
                    }
                }

                // Halt entirely when the session locks.
                if (Settings.StopWhenLocked && State != AppState.Stopped &&
                    SystemStateMonitor.IsScreenLocked())
                {
                    Dispatcher.UIThread.Post(Stop);
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        // ── Blackout check ─────────────────────────────────────────────────
        private bool IsInBlackout()
        {
            try
            {
                if (Settings.Blackouts == null) return false;
                var now = DateTime.Now;

                foreach (var b in Settings.Blackouts.Where(b => b.IsEnabled && b.IsValid))
                {
                    if (!b.EnabledDays.Contains(now.DayOfWeek)) continue;
                    var start = now.Date + b.Time;
                    var end   = start + b.Duration;
                    if (now >= start && now <= end) return true;
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
            return false;
        }

        // ── Quartz schedule management ─────────────────────────────────────
        private async Task InitialiseQuartzAsync()
        {
            try
            {
                _quartzScheduler = await new StdSchedulerFactory().GetScheduler();
                await _quartzScheduler.Start();
                await RefreshQuartzSchedulesAsync();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void RefreshQuartzSchedules() =>
            RefreshQuartzSchedulesAsync().ConfigureAwait(false);

        private async Task RefreshQuartzSchedulesAsync()
        {
            try
            {
                if (_quartzScheduler == null) return;
                await _quartzScheduler.Clear();

                if (Settings.Schedules == null) return;

                foreach (var schedule in Settings.Schedules.Where(s => s.IsEnabled && s.IsValid))
                {
                    var jobKey  = new JobKey(schedule.Id.ToString());
                    var job     = JobBuilder.Create<ScheduleArrivedJob>()
                                           .WithIdentity(jobKey)
                                           .UsingJobData("action", schedule.Action.ToString())
                                           .Build();

                    var trigger = TriggerBuilder.Create()
                                               .WithCronSchedule(schedule.CronExpression)
                                               .Build();

                    await _quartzScheduler.ScheduleJob(job, trigger);
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void OnScheduleArrived(ScheduleBase.ScheduleAction action)
        {
            if (action == ScheduleBase.ScheduleAction.Start) Start();
            else Stop();
        }

        // ── Settings persistence ───────────────────────────────────────────
        public void LoadSettings()
        {
            try
            {
                var path = StaticCode.SettingsXmlPath;
                if (!File.Exists(path)) return;

                var serializer = new XmlSerializer(typeof(Settings));
                using var stream = File.OpenRead(path);
                if (serializer.Deserialize(stream) is Settings loaded)
                    Settings = loaded;
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void SaveSettings()
        {
            try
            {
                SyncActionsToSettings();
                Settings.Schedules = Schedules.ToArray();
                Settings.Blackouts = Blackouts.ToArray();
                RefreshQuartzSchedules();
                Directory.CreateDirectory(StaticCode.WorkingDirectory);
                var path = StaticCode.SettingsXmlPath;
                var serializer = new XmlSerializer(typeof(Settings));
                using var stream = File.Create(path);
                serializer.Serialize(stream, Settings);

                // Apply launch-at-logon setting
                var exePath = Environment.ProcessPath ?? "/usr/bin/move-mouse";
                StaticCode.SetLaunchAtLogon(Settings.LaunchAtLogon, exePath);

                // Apply logging
                if (Settings.EnableLogging)
                    StaticCode.EnableLog(Settings.LogLevel);
                else
                    StaticCode.DisableLog();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private void RaiseCommandsChanged()
        {
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
