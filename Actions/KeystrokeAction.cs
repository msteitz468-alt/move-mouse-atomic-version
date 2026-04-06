using ellabi.Utilities;
using ellabi.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace ellabi.Actions
{
    public class KeystrokeAction : ActionBase
    {
        private int[]? _keystrokes;
        private int _selectedIndex = -1;
        private InputMethod _inputMethod;
        private bool _abortIfUserActivityDetected;
        private bool _pause;
        private double _pauseInterval;

        public enum InputMethod { Sequential, Simultaneous }

        public IEnumerable<InputMethod> InputMethodValues => Enum.GetValues(typeof(InputMethod)).Cast<InputMethod>();

        public int[]? Keystrokes
        {
            get => _keystrokes;
            set { _keystrokes = value; OnPropertyChanged(nameof(Keystrokes)); }
        }

        public InputMethod Method
        {
            get => _inputMethod;
            set { _inputMethod = value; OnPropertyChanged(); }
        }

        public bool AbortIfUserActivityDetected
        {
            get => _abortIfUserActivityDetected;
            set { _abortIfUserActivityDetected = value; OnPropertyChanged(); }
        }

        public bool Pause
        {
            get => _pause;
            set { _pause = value; OnPropertyChanged(); }
        }

        public double PauseInterval
        {
            get => _pauseInterval;
            set { _pauseInterval = value; OnPropertyChanged(); }
        }

        [XmlIgnore]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { _selectedIndex = value; OnPropertyChanged(); }
        }

        [XmlIgnore]
        public RelayCommand AddKeystrokeCommand { get; set; }
        [XmlIgnore]
        public RelayCommand RemoveSelectedKeystrokeCommand { get; set; }
        [XmlIgnore]
        public RelayCommand MoveUpSelectedKeystrokeCommand { get; set; }
        [XmlIgnore]
        public RelayCommand MoveDownSelectedKeystrokeCommand { get; set; }

        public override bool IsValid => _keystrokes?.Length > 0;
        public override bool CanExecute() => IsValid;

        public KeystrokeAction()
        {
            _pauseInterval = 0.1;
            InterruptsIdleTime = true;
            AddKeystrokeCommand            = new RelayCommand(_ => { }, _ => true);
            RemoveSelectedKeystrokeCommand = new RelayCommand(_ => RemoveSelectedKeystroke(), _ => SelectedIndex > -1);
            MoveUpSelectedKeystrokeCommand = new RelayCommand(_ => MoveUpSelectedKeystroke(),   _ => SelectedIndex > 0);
            MoveDownSelectedKeystrokeCommand = new RelayCommand(_ => MoveDownSelectedKeystroke(),
                _ => SelectedIndex > -1 && _keystrokes != null && SelectedIndex < _keystrokes.Length - 1);
        }

        public override void Execute()
        {
            try
            {
                IntervalExecutionCount++;
                StaticCode.Logger?.Here().Information(ToString());
                Aborted = false;

                if (_keystrokes == null || _keystrokes.Length == 0) return;

                if (Method == InputMethod.Sequential)
                {
                    var initialPos = new MouseCursorWrapper().GetCursorPosition();

                    foreach (var vk in _keystrokes)
                    {
                        if (AbortIfUserActivityDetected && initialPos != new MouseCursorWrapper().GetCursorPosition())
                        {
                            Aborted = true;
                            break;
                        }

                        var keyName = StaticCode.InputProvider?.VkToKeyName(vk) ?? vk.ToString();
                        StaticCode.InputProvider?.KeyPress(keyName);

                        if (Pause)
                            System.Threading.Thread.Sleep((int)(PauseInterval * 1000));
                    }
                }
                else // Simultaneous
                {
                    // Build a combined key string: ctrl+c+v etc.
                    var names = _keystrokes
                        .Select(vk => StaticCode.InputProvider?.VkToKeyName(vk) ?? vk.ToString())
                        .ToArray();
                    StaticCode.InputProvider?.KeyPress(string.Join("+", names));
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void AddKeystroke(int key)
        {
            try
            {
                var selectedIndex = SelectedIndex;
                var list = Keystrokes?.ToList() ?? new List<int>();
                list.Insert(SelectedIndex + 1, key);
                Keystrokes = list.ToArray();
                SelectedIndex = selectedIndex + 1;
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void RemoveSelectedKeystroke()
        {
            try
            {
                var idx = SelectedIndex;
                var list = Keystrokes?.ToList() ?? new List<int>();
                list.RemoveAt(idx);
                Keystrokes = list.ToArray();
                SelectedIndex = idx > 0 ? idx - 1 : list.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public void MoveUpSelectedKeystroke()
        {
            try
            {
                var idx = SelectedIndex;
                var list = Keystrokes?.ToList() ?? new List<int>();
                if (idx > 0) { (list[idx], list[idx - 1]) = (list[idx - 1], list[idx]); Keystrokes = list.ToArray(); SelectedIndex = idx - 1; }
            }
            catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
        }

        public void MoveDownSelectedKeystroke()
        {
            try
            {
                var idx = SelectedIndex;
                var list = Keystrokes?.ToList() ?? new List<int>();
                if (idx < list.Count - 1) { (list[idx], list[idx + 1]) = (list[idx + 1], list[idx]); Keystrokes = list.ToArray(); SelectedIndex = idx + 1; }
            }
            catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
        }

        public override string ToString() =>
            $"{GetType().Name} | Name={Name} | Method={Method} | Keys={string.Join(",", _keystrokes ?? Array.Empty<int>())} | Trigger={Trigger}";
    }
}
