using MudBlazor.Services;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Infrastructure.ServiceBus;
using Vibes.ASBManager.Infrastructure.Storage;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddScoped<ProtectedLocalStorage>();

// Connection store (prefer PostgreSQL via Aspire connection string; fallback to JSON file)
var pgConn = builder.Configuration.GetConnectionString("asbdb");
if (!string.IsNullOrWhiteSpace(pgConn))
{
    builder.Services.AddSingleton<IConnectionStore>(_ => new SqlConnectionStore(pgConn));
}
else
{
    // Prefer a configurable data directory for Docker volume mapping.
    // 1) If ASB_DATA_DIR is set, use that directory
    // 2) Else, if running in container, default to /app/data (writable by app user in official .NET images)
    // 3) Else, use ContentRoot/App_Data
    var configuredDataDir = builder.Configuration["ASB_DATA_DIR"];
    var inContainer = builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");
    var dataDir = !string.IsNullOrWhiteSpace(configuredDataDir)
        ? configuredDataDir!
        : (inContainer ? "/app/data" : Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
    Directory.CreateDirectory(dataDir);
    var dataPath = Path.Combine(dataDir, "connections.json");

    // One-time migration: if the new path doesn't exist, but the legacy App_Data file does, copy it
    try
    {
        var legacyPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "connections.json");
        if (!File.Exists(dataPath) && File.Exists(legacyPath))
        {
            File.Copy(legacyPath, dataPath, overwrite: false);
        }
    }
    catch
    {
        // best-effort migration; ignore errors
    }
    builder.Services.AddSingleton<IConnectionStore>(_ => new JsonConnectionStore(dataPath));
}

// Service Bus services
builder.Services.AddSingleton<IServiceBusAdmin, AzureServiceBusAdmin>();
builder.Services.AddSingleton<IServiceBusMessaging, AzureServiceBusMessaging>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
