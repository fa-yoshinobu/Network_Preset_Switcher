using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace NetworkPresetSwitcher.Infrastructure;

public static class Localization
{
    private const string ResourcePrefix = "Resources/Strings.";
    private static readonly string[] SupportedCultures = { "ja-JP", "en-US" };

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguage { get; private set; } = "en-US";

    public static CultureInfo CurrentCultureInfo { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public static void Initialize()
    {
        SetLanguage(CurrentLanguage);
    }

    public static void ToggleLanguage()
    {
        var next = CurrentLanguage == "ja-JP" ? "en-US" : "ja-JP";
        SetLanguage(next);
    }

    public static void SetLanguage(string culture)
    {
        if (!SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            culture = "en-US";
        }

        CurrentLanguage = culture;
        CurrentCultureInfo = CultureInfo.GetCultureInfo(culture);

        Thread.CurrentThread.CurrentCulture = CurrentCultureInfo;
        Thread.CurrentThread.CurrentUICulture = CurrentCultureInfo;

        if (Application.Current == null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source != null &&
            d.Source.OriginalString.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            dictionaries.Remove(existing);
        }

        var dict = new ResourceDictionary
        {
            Source = new Uri($"{ResourcePrefix}{culture}.xaml", UriKind.Relative)
        };

        dictionaries.Add(dict);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key)
    {
        if (Application.Current?.Resources[key] is string value)
        {
            return value;
        }

        return key;
    }

    public static string Format(string key, params object[] args)
    {
        var format = T(key);
        return string.Format(CurrentCultureInfo, format, args);
    }
}

