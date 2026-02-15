using System;

namespace NetworkPresetSwitcher.ViewModels;

public sealed class ActivityItem
{
    public ActivityItem(string title, string detail, ActivityLevel level = ActivityLevel.Info)
    {
        Timestamp = DateTime.Now;
        Title = title;
        Detail = detail;
        Level = level;
    }

    public DateTime Timestamp { get; }

    public string Title { get; }

    public string Detail { get; }

    public ActivityLevel Level { get; }

    public string TimeText => Timestamp.ToString("HH:mm");
}

public enum ActivityLevel
{
    Info,
    Success,
    Warning,
    Error
}

