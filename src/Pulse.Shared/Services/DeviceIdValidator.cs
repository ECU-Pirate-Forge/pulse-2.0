using System;

namespace Pulse.Shared.Services;

public static class DeviceIdValidator
{
    public static bool IsValid(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return Guid.TryParse(deviceId, out _);
    }
}