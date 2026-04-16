namespace Pulse.Application.Services;

public sealed record DeviceIdValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static DeviceIdValidationResult Success() =>
        new() { IsValid = true };

    public static DeviceIdValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

public class DeviceIdValidationService
{
    public DeviceIdValidationResult ValidateDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return DeviceIdValidationResult.Failure("DeviceId is required.");

        if (!Guid.TryParse(deviceId, out _))
            return DeviceIdValidationResult.Failure("DeviceId must be a valid UUID.");

        return DeviceIdValidationResult.Success();
    }
}
