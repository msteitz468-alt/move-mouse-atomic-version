using Avalonia.Data.Converters;
using Avalonia.Media;
using ellabi.ViewModels;
using System;
using System.Globalization;

namespace ellabi.ViewModels
{
    /// <summary>Maps the visual status to the status-ring colour.</summary>
    public class StateToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is MainWindowViewModel.VisualStatus status
                ? status switch
                {
                    MainWindowViewModel.VisualStatus.Running   => Color.FromRgb(0xA6, 0xE3, 0xA1), // green
                    MainWindowViewModel.VisualStatus.Executing => Color.FromRgb(0xF3, 0x8B, 0xA8), // red
                    MainWindowViewModel.VisualStatus.Paused    => Color.FromRgb(0xF9, 0xE2, 0xAF), // yellow
                    MainWindowViewModel.VisualStatus.Sleeping  => Color.FromRgb(0xCB, 0xA6, 0xF7), // purple
                    MainWindowViewModel.VisualStatus.Battery   => Color.FromRgb(0xFA, 0xB3, 0x87), // orange
                    _                                          => Color.FromRgb(0x89, 0xB4, 0xFA)  // blue (idle)
                }
                : Color.FromRgb(0x6C, 0x70, 0x86);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
