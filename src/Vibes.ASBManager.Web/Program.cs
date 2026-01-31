using MudBlazor.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Vibes.ASBManager.Infrastructure.AzureServiceBus;
using Vibes.ASBManager.Infrastructure.Storage.File;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddScoped<ProtectedLocalStorage>();

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
try { Directory.CreateDirectory(dataDir); } catch { }
var jsonPath = Path.Combine(dataDir, "connections.json");

builder.Services.AddFileStorage(jsonPath);
builder.Services.AddAzureServiceBusInfrastructure();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();

app.MapStaticAssets();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
