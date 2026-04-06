using System;
using System.Diagnostics;
using System.IO;

namespace ellabi.Actions
{
    /// <summary>
    /// Linux version: runs .sh shell scripts via /bin/bash instead of PowerShell .ps1 files.
    /// </summary>
    public class ScriptAction : ActionBase
    {
        private const string BashPath = "/bin/bash";

        private string? _scriptPath;
        private bool _waitForExit;
        private bool _hidden;

        public override bool IsValid =>
            !string.IsNullOrWhiteSpace(ScriptPath) &&
            File.Exists(ScriptPath) &&
            ScriptPath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase);

        public string? ScriptPath
        {
            get => _scriptPath;
            set { _scriptPath = value; OnPropertyChanged(); }
        }

        public bool WaitForExit
        {
            get => _waitForExit;
            set { _waitForExit = value; OnPropertyChanged(); }
        }

        public bool Hidden
        {
            get => _hidden;
            set { _hidden = value; OnPropertyChanged(); }
        }

        public override bool CanExecute() => IsValid;

        public override void Execute()
        {
            try
            {
                IntervalExecutionCount++;
                StaticCode.Logger?.Here().Information(ToString());

                if (!File.Exists(BashPath) || !File.Exists(ScriptPath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = BashPath,
                    Arguments = $"\"{ScriptPath!.Trim()}\"",
                    UseShellExecute = !Hidden,
                    CreateNoWindow = Hidden,
                    RedirectStandardOutput = false
                };

                var process = new Process { StartInfo = psi };
                process.Start();
                if (WaitForExit) process.WaitForExit();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        public override string ToString() =>
            $"{GetType().Name} | Name={Name} | ScriptPath={ScriptPath} | WaitForExit={WaitForExit} | Trigger={Trigger}";
    }
}
