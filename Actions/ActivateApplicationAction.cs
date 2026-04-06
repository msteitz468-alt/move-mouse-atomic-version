using ellabi.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace ellabi.Actions
{
    public class ActivateApplicationAction : ActionBase
    {
        private SearchMode _mode;
        private string? _application;

        public enum SearchMode { Process, Window }

        public IEnumerable<SearchMode> SearchModeValues => Enum.GetValues(typeof(SearchMode)).Cast<SearchMode>();

        public override bool IsValid => !string.IsNullOrWhiteSpace(Application);

        public SearchMode Mode
        {
            get => _mode;
            set { _mode = value; Application = null; OnPropertyChanged(); RefreshApplications(); }
        }

        public string? Application
        {
            get => _application;
            set { _application = value; OnPropertyChanged(); }
        }

        [XmlIgnore]
        public RelayCommand RefreshApplicationsCommand { get; set; }

        [XmlIgnore]
        public List<string> AvailableApplications
        {
            get
            {
                var list = new List<string>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(Application))
                        list.Add(Application);

                    foreach (var proc in Process.GetProcesses().Where(p => p.Responding))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                            {
                                var name = Mode == SearchMode.Window ? proc.MainWindowTitle : proc.ProcessName;
                                if (!list.Contains(name)) list.Add(name);
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    StaticCode.Logger?.Here().Error(ex.Message);
                }
                return list.OrderBy(a => a).ToList();
            }
        }

        public ActivateApplicationAction()
        {
            RefreshApplicationsCommand = new RelayCommand(_ => RefreshApplications());
        }

        public override bool CanExecute() => IsValid;

        public override void Execute()
        {
            try
            {
                IntervalExecutionCount++;
                StaticCode.Logger?.Here().Information(ToString());

                string? windowName = null;

                if (Mode == SearchMode.Window && Application != null &&
                    (Application.StartsWith("*") || Application.EndsWith("*")))
                {
                    var trimmed = Application.Trim('*');
                    windowName = Process.GetProcesses().FirstOrDefault(p =>
                    {
                        if (string.IsNullOrEmpty(p.MainWindowTitle)) return false;
                        if (Application.StartsWith("*") && Application.EndsWith("*"))
                            return p.MainWindowTitle.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
                        if (Application.StartsWith("*"))
                            return p.MainWindowTitle.EndsWith(trimmed, StringComparison.OrdinalIgnoreCase);
                        return p.MainWindowTitle.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase);
                    })?.MainWindowTitle;
                }
                else
                {
                    windowName = Mode == SearchMode.Window
                        ? Application
                        : Process.GetProcessesByName(Application!)
                                 .FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                                 ?.MainWindowTitle;
                }

                if (!string.IsNullOrEmpty(windowName))
                    StaticCode.InputProvider?.ActivateWindow(windowName);
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void RefreshApplications()
        {
            try { OnPropertyChanged(nameof(AvailableApplications)); }
            catch (Exception ex) { StaticCode.Logger?.Here().Error(ex.Message); }
        }

        public override string ToString() =>
            $"{GetType().Name} | Name={Name} | Mode={Mode} | Application={Application} | Trigger={Trigger}";
    }
}
