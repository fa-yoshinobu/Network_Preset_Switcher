using System.Collections.Generic;

namespace NetworkPresetSwitcher.ViewModels;

/// <summary>One row of the "current → after apply" comparison.</summary>
public sealed class ApplyDiffItem
{
    public ApplyDiffItem(string label, string current, string after, bool isChanged)
    {
        Label = label;
        Current = current;
        After = after;
        IsChanged = isChanged;
    }

    public string Label { get; }

    public string Current { get; }

    public string After { get; }

    public bool IsChanged { get; }
}

/// <summary>Everything the apply confirmation dialog shows.</summary>
public sealed class ApplyConfirmation
{
    public ApplyConfirmation(string presetName, string adapterName, bool isWireless, IReadOnlyList<ApplyDiffItem> items)
    {
        PresetName = presetName;
        AdapterName = adapterName;
        IsWireless = isWireless;
        Items = items;
    }

    public string PresetName { get; }

    public string AdapterName { get; }

    public bool IsWireless { get; }

    public IReadOnlyList<ApplyDiffItem> Items { get; }
}
