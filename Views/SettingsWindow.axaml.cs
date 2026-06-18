using Avalonia.Controls;
using Avalonia.Interactivity;
using ellabi.ViewModels;
using System;
using System.Diagnostics;

namespace ellabi.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            // Persist settings however the window is closed (button, title-bar X, Esc).
            Closing += (_, _) => (DataContext as MainWindowViewModel)?.SaveSettings();
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            // The button's Command performs the save; give a brief visual confirmation.
            if (sender is Button button)
            {
                var original = button.Content;
                button.Content = "Saved ✓";
                await System.Threading.Tasks.Task.Delay(1200);
                button.Content = original;
            }
        }

        private void OnGitHubClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(StaticCode.GitHubUrl)
                    { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }
        }

        private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
