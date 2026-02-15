using System;
using System.Globalization;
using System.Windows.Data;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.Converters;

public sealed class GroupNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Localization.T("Preset.Group.Ungrouped");
        }

        return text.Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

