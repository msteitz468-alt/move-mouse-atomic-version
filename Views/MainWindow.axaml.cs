using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using ellabi.ViewModels;
using System;
using System.Diagnostics;

namespace ellabi.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainWindowViewModel();
            DataContext = _vm;

            // Build system tray
            if (!_vm.Settings.HideSystemTrayIcon)
                SetupTrayIcon();

            // Hide to tray instead of close
            Closing += (_, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        private void SetupTrayIcon()
        {
            var trayIcon = new TrayIcon
            {
                ToolTipText = "Move Mouse",
                IsVisible = true
            };

            var menu = new NativeMenu();

            var showItem = new NativeMenuItem("Show");
            showItem.Click += (_, _) => { Show(); Activate(); };
            menu.Add(showItem);

            menu.Add(new NativeMenuItemSeparator());

            var startItem = new NativeMenuItem("Start");
            startItem.Click += (_, _) => _vm?.Start();
            menu.Add(startItem);

            var pauseItem = new NativeMenuItem("Pause");
            pauseItem.Click += (_, _) => _vm?.Pause();
            menu.Add(pauseItem);

            var stopItem = new NativeMenuItem("Stop");
            stopItem.Click += (_, _) => _vm?.Stop();
            menu.Add(stopItem);

            menu.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += (_, _) =>
            {
                _vm?.Stop();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
                    lt.Shutdown();
            };
            menu.Add(quitItem);

            trayIcon.Menu = menu;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Hide();

        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { DataContext = _vm };
            settingsWindow.ShowDialog(this);
        }

        private void OnViewUpdateClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var url = StaticCode.UpdateUrl;
                if (!string.IsNullOrWhiteSpace(url))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
