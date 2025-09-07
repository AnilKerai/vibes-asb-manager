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

// Connection store (JSON file under App_Data)
var dataDir = System.IO.Path.Combine(builder.Environment.ContentRootPath, "App_Data");
try { System.IO.Directory.CreateDirectory(dataDir); } catch { }
var jsonPath = System.IO.Path.Combine(dataDir, "connections.json");
builder.Services.AddSingleton<IConnectionStore>(_ => new JsonConnectionStore(jsonPath));

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
