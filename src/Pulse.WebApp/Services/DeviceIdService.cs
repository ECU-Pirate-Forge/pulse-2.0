using Microsoft.JSInterop;

namespace Pulse.WebApp.Services;

public interface IDeviceIdService
{
    Task<string> GetDeviceIdAsync();
}

public class DeviceIdService : IDeviceIdService
{
    private readonly IJSRuntime _js;
    private const string Key = "pulse_device_id";

    public DeviceIdService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string> GetDeviceIdAsync()
    {
        var id = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            await _js.InvokeVoidAsync("localStorage.setItem", Key, id);
        }
        return id;
    }
}
