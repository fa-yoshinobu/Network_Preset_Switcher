using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NetworkPresetSwitcher.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool flag && flag)
        {
            return TrueBrush ?? Brushes.Transparent;
        }

        return FalseBrush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

