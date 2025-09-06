using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Infrastructure.ServiceBus;
using Vibes.ASBManager.Infrastructure.Storage;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddScoped<ProtectedLocalStorage>();

// App data path for connection store
var dataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "connections.json");
builder.Services.AddSingleton<IConnectionStore>(_ => new JsonConnectionStore(dataPath));

// Service Bus services
builder.Services.AddSingleton<IServiceBusAdmin, AzureServiceBusAdmin>();
builder.Services.AddSingleton<IServiceBusMessaging, AzureServiceBusMessaging>();

var app = builder.Build();

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
