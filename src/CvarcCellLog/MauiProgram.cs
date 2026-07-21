using CommunityToolkit.Maui;
using CvarcCellLog.Pages;
using CvarcCellLog.Platforms.Android;
using CvarcCellLog.Services;
using CvarcCellLog.ViewModels;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Lookup;
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
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		string dbPath = DatabasePathService.ResolveActivePath();
		builder.Services.AddDbContext<CvarcLoggerDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

		builder.Services.AddSingleton<IAppRestartService, AndroidAppRestartService>();

		builder.Services.AddSingleton<IClock, SystemClock>();
		builder.Services.AddScoped<IQsoRepository, QsoRepository>();
		builder.Services.AddScoped<IStationProfileRepository, StationProfileRepository>();
		builder.Services.AddScoped<IDxccEntityRepository, DxccEntityRepository>();
		builder.Services.AddSingleton<ICallsignEntityResolver, CallsignEntityResolver>();
		builder.Services.AddSingleton<IGridZoneResolver, GridZoneResolver>();
		builder.Services.AddScoped<ISotaActivationRepository, SotaActivationRepository>();
		builder.Services.AddScoped<IPotaActivationRepository, PotaActivationRepository>();

		builder.Services.AddSingleton<ICredentialStore, SecureStorageCredentialStore>();
		builder.Services.AddHttpClient<CallookLookupService>();
		builder.Services.AddHttpClient<QrzLookupService>();
		builder.Services.AddHttpClient<QrzCqLookupService>();
		builder.Services.AddScoped<LookupCoordinator>();
		builder.Services.AddHttpClient<SotaSummitLookupService>();
		builder.Services.AddHttpClient<PotaParkLookupService>();

		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<LoadingPage>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<FileMenuPage>();
		builder.Services.AddTransient<StationProfilesPage>();
		builder.Services.AddTransient<StationProfileViewModel>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<QsoLogPage>();
		builder.Services.AddTransient<QsoLogViewModel>();
		builder.Services.AddTransient<QsoEntryPage>();
		builder.Services.AddTransient<QsoEntryViewModel>();
		builder.Services.AddTransient<QsoEditPage>();
		builder.Services.AddTransient<QsoEditViewModel>();
		builder.Services.AddTransient<LogColumnsPage>();
		builder.Services.AddTransient<LogColumnsViewModel>();
		builder.Services.AddTransient<AwardsPage>();
		builder.Services.AddTransient<AwardsViewModel>();
		builder.Services.AddTransient<MountainGoatViewModel>();
		builder.Services.AddTransient<ParksOnTheAirViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
