using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher.Converters;

public sealed class ActivityLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is ActivityLevel activityLevel ? activityLevel : ActivityLevel.Info;
        var key = level switch
        {
            ActivityLevel.Success => "BrushSuccess",
            ActivityLevel.Warning => "BrushWarning",
            ActivityLevel.Error => "BrushDanger",
            _ => "BrushInfo"
        };

        if (Application.Current?.Resources[key] is Brush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

