using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Matrix.Models;

namespace Matrix.Converters
{
    public class UnitConverter : IValueConverter
    {
        public static readonly UnitConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ComponentType type)
            {
                switch (type)
                {
                    case ComponentType.SeriesInductor:
                    case ComponentType.ShuntInductor:
                        return "nH";
                    case ComponentType.SeriesCapacitor:
                    case ComponentType.ShuntCapacitor:
                        return "pF";
                }
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}