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
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

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
