using CvarcCellLog.Pages;
using CvarcCellLog.ViewModels;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Data;
using CvarcLogger.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CvarcCellLog;

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

		string dbPath = Path.Combine(FileSystem.AppDataDirectory, "cvarclogger.db");
		builder.Services.AddDbContext<CvarcLoggerDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

		builder.Services.AddSingleton<IClock, SystemClock>();
		builder.Services.AddScoped<IQsoRepository, QsoRepository>();
		builder.Services.AddScoped<IStationProfileRepository, StationProfileRepository>();
		builder.Services.AddScoped<IDxccEntityRepository, DxccEntityRepository>();
		builder.Services.AddSingleton<ICallsignEntityResolver, CallsignEntityResolver>();
		builder.Services.AddSingleton<IGridZoneResolver, GridZoneResolver>();

		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<LoadingPage>();
		builder.Services.AddTransient<QsoLogPage>();
		builder.Services.AddTransient<QsoLogViewModel>();
		builder.Services.AddTransient<QsoEntryPage>();
		builder.Services.AddTransient<QsoEntryViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
