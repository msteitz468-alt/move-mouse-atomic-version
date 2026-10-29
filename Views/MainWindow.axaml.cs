using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
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
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt && lt.ShutdownMode == ShutdownMode.OnMainWindowClose)
                {
                    // Allow shutdown if it's initiated explicitly
                }
                else
                {
                    e.Cancel = true;
                    Hide();
                }
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

        // A left press toggles Start/Stop only if it is released without moving; moving
        // the pointer past this many pixels turns it into a window drag instead.
        private const double DragThreshold = 4;
        private PointerPressedEventArgs? _pressArgs;
        private Point _pressPoint;

        public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            // Ignore the second press of a double-click so it doesn't toggle twice.
            _pressArgs = e.ClickCount == 1 ? e : null;
            _pressPoint = e.GetPosition(this);
        }

        public void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_pressArgs == null) return;

            var delta = e.GetPosition(this) - _pressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

            var press = _pressArgs;
            _pressArgs = null; // a drag, not a click
            BeginMoveDrag(press);
        }

        public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pressArgs == null || e.InitialPressMouseButton != MouseButton.Left) return;
            _pressArgs = null;

            if (_vm == null) return;
            if (_vm.IsRunning)
                _vm.Stop();
            else
                _vm.Start();
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { DataContext = _vm };
            settingsWindow.Show();
        }

        private void OnCloseContextClick(object? sender, RoutedEventArgs e)
        {
            _vm?.Stop();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
                lt.Shutdown();
        }

        private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
