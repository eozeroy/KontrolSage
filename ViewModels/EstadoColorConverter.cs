using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KontrolSage.Models;

namespace KontrolSage.ViewModels
{
    /// <summary>
    /// Devuelve un color de fondo según el EstadoCostoReal para la columna Estado en el DataGrid.
    /// </summary>
    public class EstadoColorConverter : IValueConverter
    {
        public static readonly EstadoColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is EstadoCostoReal estado)
            {
                return estado switch
                {
                    EstadoCostoReal.Borrador      => Color.Parse("#6B7280"),   // gris
                    EstadoCostoReal.Aprobado      => Color.Parse("#16A34A"),   // verde
                    EstadoCostoReal.Reclasificado => Color.Parse("#D97706"),   // ámbar
                    _                             => Color.Parse("#6B7280")
                };
            }
            return Color.Parse("#6B7280");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
