using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KontrolSage.ViewModels
{
    /// <summary>
    /// Convierte un Avalonia.Media.Color a un IBrush para usar en backgrounds XAML.
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
                return new SolidColorBrush(color);
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
