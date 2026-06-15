using System;

namespace NetworkPresetSwitcher.Models;

public sealed class NetworkPresetApplyException : Exception
{
    public NetworkPresetApplyException(string message)
        : base(message)
    {
    }

    public NetworkPresetApplyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
