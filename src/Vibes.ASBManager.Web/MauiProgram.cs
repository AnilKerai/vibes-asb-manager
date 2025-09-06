using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Infrastructure.ServiceBus;
using Vibes.ASBManager.Infrastructure.Storage;
#if MACCATALYST
#endif

namespace Vibes.ASBManager.Web;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register services
		builder.Services.AddSingleton<IConnectionStore>(sp =>
		{
			var path = Path.Combine(FileSystem.AppDataDirectory, "connections.json");
			return new JsonConnectionStore(path);
		});
		builder.Services.AddSingleton<IServiceBusAdmin, AzureServiceBusAdmin>();
		builder.Services.AddSingleton<IServiceBusMessaging, AzureServiceBusMessaging>();

		// Blazor + MudBlazor
		builder.Services.AddMauiBlazorWebView();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif
		builder.Services.AddMudServices();

        // Note: On MacCatalyst, Tab navigation in BlazorWebView has platform limitations.
        // We do not attempt to modify WKWebView preferences here due to API availability differences.

		var app = builder.Build();
		return app;
	}
}
