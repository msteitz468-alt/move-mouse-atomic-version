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
                // Initialise platform input provider
                StaticCode.InputProvider = new XdotoolInputProvider();

                // Initialise directories and logging
                Directory.CreateDirectory(StaticCode.WorkingDirectory);
                Directory.CreateDirectory(StaticCode.TempDirectory);
                StaticCode.CreateLog();
                StaticCode.EnableLog(LogEventLevel.Debug);

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                // Hide the window at startup; tray icon is the primary entry point
                desktop.MainWindow.Show();
            }

            base.OnFrameworkInitializationCompleted();
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
