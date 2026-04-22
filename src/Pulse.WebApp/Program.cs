using MudBlazor.Services;
using Pulse.WebApp.Components;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Configure HttpClient with BaseAddress for API calls
builder.Services.AddScoped<HttpClient>(sp => 
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    
    // For development: construct API URL based on current host
    // Replace port 5243 (WebApp) with 5062 (WebApi)
    var apiBaseUrl = navigationManager.BaseUri.Replace(":5243", ":5062");
    
    return new HttpClient 
    { 
        BaseAddress = new Uri(apiBaseUrl)
    };
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
