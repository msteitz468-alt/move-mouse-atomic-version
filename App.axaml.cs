using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ellabi.Platform;
using ellabi.Views;
using Serilog.Events;
using System;
using System.IO;

namespace ellabi
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Initialise directories and logging first so provider selection can log.
                Directory.CreateDirectory(StaticCode.WorkingDirectory);
                Directory.CreateDirectory(StaticCode.TempDirectory);
                StaticCode.CreateLog();
                StaticCode.EnableLog(LogEventLevel.Debug);

                // Initialise platform input provider, choosing the backend that suits
                // the current display server. Wayland blocks X11-style automation, so
                // we drive input through ydotool/uinput there instead of xdotool.
                StaticCode.InputProvider = SelectInputProvider();

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                // Hide the window at startup; tray icon is the primary entry point
                desktop.MainWindow.Show();
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Pick the input backend. Honours an explicit MOVEMOUSE_INPUT=xdotool|ydotool
        /// override; otherwise uses ydotool on Wayland sessions and xdotool on X11.
        /// </summary>
        private static IInputProvider SelectInputProvider()
        {
            var forced = Environment.GetEnvironmentVariable("MOVEMOUSE_INPUT")?.Trim().ToLowerInvariant();
            var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Trim().ToLowerInvariant();

            bool useYdotool = forced switch
            {
                "ydotool" => true,
                "xdotool" => false,
                _ => sessionType == "wayland"
            };

            StaticCode.Logger?.Here().Information(
                "Selecting input backend: {Backend} (XDG_SESSION_TYPE={Session}, MOVEMOUSE_INPUT={Forced})",
                useYdotool ? "ydotool" : "xdotool", sessionType ?? "(unset)", forced ?? "(unset)");

            return useYdotool ? new YdotoolInputProvider() : new XdotoolInputProvider();
        }

        [STAThread]
        public static int Main(string[] args)
        {
            var builder = BuildAvaloniaApp();
            return builder.StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .LogToTrace();
    }
}
