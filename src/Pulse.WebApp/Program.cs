using MudBlazor.Services;
using Pulse.WebApp.Components;
using Pulse.WebApp.Services;
using Pulse.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddScoped<IDeviceIdService, DeviceIdService>();

builder.Services.AddHttpClient("webapi", client =>
{
    var baseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "https+http://webapi";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("webapi"));
builder.Services.AddScoped(sp => new SessionRealtimeConnectionFactory(
    builder.Configuration["WebApi:HubBaseUrl"]
        ?? builder.Configuration["WebApi:BaseUrl"]
        ?? "http://webapi",
    () => sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("webapi")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UsePulseDevTunnels();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
