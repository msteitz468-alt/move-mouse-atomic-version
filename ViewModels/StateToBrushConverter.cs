using Avalonia.Data.Converters;
using Avalonia.Media;
using ellabi.ViewModels;
using System;
using System.Globalization;

namespace ellabi.ViewModels
{
    /// <summary>Maps AppState enum to a status indicator colour.</summary>
    public class StateToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is MainWindowViewModel.AppState state
                ? state switch
                {
                    MainWindowViewModel.AppState.Running => Color.FromRgb(0xA6, 0xE3, 0xA1), // green
                    MainWindowViewModel.AppState.Paused  => Color.FromRgb(0xF9, 0xE2, 0xAF), // yellow
                    _                                    => Color.FromRgb(0xF3, 0x8B, 0xA8)  // red
                }
                : Color.FromRgb(0x6C, 0x70, 0x86);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
