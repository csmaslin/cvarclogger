using CvarcLogger.Data;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Data.Repositories;
using CvarcLogger.App.Services;
using CvarcLogger.App.Platform;
using CvarcLogger.Core.Lookup;
using Serilog;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add CORS policy for prototype
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPrototype", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register core abstractions
builder.Services.AddSingleton<IClock, SystemClock>();

// Register data access layer
builder.Services.AddDbContext<CvarcLoggerDbContext>(options =>
{
    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CvarcLogger", "cvarclogger.db");
    options.UseSqlite($"Data Source={dbPath}");
});
builder.Services.AddScoped<IQsoRepository, QsoRepository>();
builder.Services.AddScoped<IStationProfileRepository, StationProfileRepository>();
builder.Services.AddScoped<IDxccEntityRepository, DxccEntityRepository>();
builder.Services.AddScoped<ISotaActivationRepository, SotaActivationRepository>();
builder.Services.AddScoped<IPotaActivationRepository, PotaActivationRepository>();

// Register application services
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<InternetCatCoordinator>();

// Register lookup services and dependencies
builder.Services.AddScoped<ICredentialStore, DpapiCredentialStore>();
builder.Services.AddHttpClient<CallookLookupService>();
builder.Services.AddHttpClient<QrzLookupService>();
builder.Services.AddHttpClient<QrzCqLookupService>();
builder.Services.AddScoped<LookupCoordinator>();
builder.Services.AddScoped<LookupCoordinator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowPrototype");
app.UseAuthorization();
app.MapControllers();

Log.Information("CvarcLogger Web API starting...");
app.Run();

public partial class Program { }
