using System.Collections;
using System.Globalization;
using AppLocalization = NetworkPresetSwitcher.Infrastructure.Localization;
using NetworkPresetSwitcher.Models;

namespace NetworkPresetSwitcher.Services;

internal sealed class PresetNaturalComparer : IComparer
{
    public int Compare(object? x, object? y)
    {
        var leftPreset = x as NetworkPreset;
        var rightPreset = y as NetworkPreset;
        var leftGroup = NormalizeGroup(leftPreset?.Group);
        var rightGroup = NormalizeGroup(rightPreset?.Group);

        var groupCompare = StringComparer.CurrentCultureIgnoreCase.Compare(leftGroup, rightGroup);
        if (groupCompare != 0)
        {
            return groupCompare;
        }

        var leftName = leftPreset?.Name ?? string.Empty;
        var rightName = rightPreset?.Name ?? string.Empty;
        return CompareNatural(leftName, rightName);
    }

    private static string NormalizeGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return AppLocalization.T("Preset.Group.Ungrouped");
        }

        return group.Trim();
    }

    private static int CompareNatural(string left, string right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
        var ix = 0;
        var iy = 0;

        while (ix < left.Length && iy < right.Length)
        {
            var cx = left[ix];
            var cy = right[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                var startX = ix;
                while (ix < left.Length && char.IsDigit(left[ix])) ix++;
                var startY = iy;
                while (iy < right.Length && char.IsDigit(right[iy])) iy++;

                var numX = left.Substring(startX, ix - startX);
                var numY = right.Substring(startY, iy - startY);

                var numXTrim = numX.TrimStart('0');
                var numYTrim = numY.TrimStart('0');

                var lenX = numXTrim.Length;
                var lenY = numYTrim.Length;
                if (lenX != lenY)
                {
                    return lenX.CompareTo(lenY);
                }

                var cmp = string.CompareOrdinal(numXTrim, numYTrim);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = numX.Length.CompareTo(numY.Length);
                if (cmp != 0)
                {
                    return cmp;
                }

                continue;
            }

            var segStartX = ix;
            while (ix < left.Length && !char.IsDigit(left[ix])) ix++;
            var segStartY = iy;
            while (iy < right.Length && !char.IsDigit(right[iy])) iy++;

            var segX = left.Substring(segStartX, ix - segStartX);
            var segY = right.Substring(segStartY, iy - segStartY);

            var segCmp = compareInfo.Compare(segX, segY, CompareOptions.IgnoreCase);
            if (segCmp != 0)
            {
                return segCmp;
            }
        }

        if (ix < left.Length)
        {
            return 1;
        }

        if (iy < right.Length)
        {
            return -1;
        }

        return 0;
    }
}
