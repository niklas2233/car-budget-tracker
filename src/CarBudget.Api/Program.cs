using CarBudget.Api.Controllers;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using CarBudget.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

var dataDirectoryPath = ResolveDataDirectory(runningInContainer);
Directory.CreateDirectory(dataDirectoryPath);

var configFilePath = Path.Combine(dataDirectoryPath, "carbudget.config.json");
var fileConfig = LoadFileConfig(configFilePath);

var environmentRegion = Environment.GetEnvironmentVariable("region");
var effectiveRegion = NormalizeRegion(!string.IsNullOrWhiteSpace(environmentRegion)
    ? environmentRegion
    : fileConfig?.Region);

var webUiPort = Environment.GetEnvironmentVariable("webui_port");
if (!string.IsNullOrWhiteSpace(webUiPort))
{
    builder.WebHost.UseUrls($"http://+:{webUiPort}");
}
else if (runningInContainer)
{
    builder.WebHost.UseUrls("http://+:2233");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Database connection: env vars take priority (Docker/advanced), otherwise default SQLite in data dir.
var databaseProvider = Environment.GetEnvironmentVariable("CARBUDGET_DB_PROVIDER") ?? "Sqlite";

var defaultSqliteConnectionString = $"Data Source={Path.Combine(dataDirectoryPath, "carbudget.db")}";

var sqliteConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SqliteConnection")
    ?? defaultSqliteConnectionString;

var postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
    ?? "Host=localhost;Port=5432;Database=carbudget;Username=postgres;Password=postgres";

// When the region env var is provided (e.g. Docker) and no config file exists yet,
// write one automatically so the setup page is skipped on every start.
if (!string.IsNullOrWhiteSpace(environmentRegion) && !File.Exists(configFilePath))
{
    try
    {
        File.WriteAllText(configFilePath, JsonSerializer.Serialize(
            new AppFileConfig { Region = effectiveRegion },
            new JsonSerializerOptions { WriteIndented = true }));
    }
    catch { }
}

var setupRequiredCheck = () =>
    !File.Exists(configFilePath)
    && string.IsNullOrWhiteSpace(environmentRegion);

builder.Services.AddDbContext<CarBudgetDbContext>(options =>
{
    if (string.Equals(databaseProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(postgresConnectionString);
    }
    else
    {
        options.UseSqlite(sqliteConnectionString);
    }
});

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

var debugEnv = Environment.GetEnvironmentVariable("debug");
var debugFromEnv = string.Equals(debugEnv, "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(debugEnv, "1", StringComparison.OrdinalIgnoreCase);

var currencyEnv = Environment.GetEnvironmentVariable("currency")?.Trim().ToUpperInvariant();

// activeCurrency is mutable so the POST handler can update it at runtime.
// Env var takes priority; falls back to config file, then null (= derive from region in frontend).
string? activeCurrency = currencyEnv ?? fileConfig?.Currency?.Trim().ToUpperInvariant();

VehiclesController.Region = effectiveRegion;
VehiclesController.LogFilePath = Path.Combine(dataDirectoryPath, "lookup.log");
VehiclesController.DebugSaveHtml = debugFromEnv || (fileConfig?.Debug?.SavePlaywrightHtml ?? false);

static string NormalizeRegion(string? value)
{
    var region = (value ?? string.Empty).Trim().ToLowerInvariant();
    return region switch
    {
        "norway"  => "norway",
        "europe"  => "europe",
        "america" => "america",
        "usa"     => "usa",
        "gb"      => "gb",
        _         => "sweden"
    };
}

var appLogPath = Path.Combine(dataDirectoryPath, "app.log");

void AppLog(string message)
{
    try { File.AppendAllText(appLogPath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}\n"); } catch { }
}

AppLog($"=== Startup ===");
AppLog($"DataDir:       {dataDirectoryPath}");
AppLog($"DbProvider:    {databaseProvider}");
AppLog($"DbPath:        {sqliteConnectionString}");
AppLog($"Region:        {effectiveRegion}");
AppLog($"ConfigExists:  {File.Exists(configFilePath)}");
AppLog($"SetupRequired: {setupRequiredCheck()}");
AppLog($"Container:     {runningInContainer}");
AppLog($"DebugSaveHtml: {VehiclesController.DebugSaveHtml}");
AppLog($"Currency:      {activeCurrency ?? "(derived from region)"}");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CarBudgetDbContext>();
        InitializeDatabase(db);
    }
    AppLog("InitializeDatabase: OK");
}
catch (Exception ex)
{
    AppLog($"InitializeDatabase: FAILED — {ex}");
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

app.UseExceptionHandler(errApp => errApp.Run(async context =>
{
    var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (ex != null)
    {
        AppLog($"Unhandled exception [{context.Request.Method} {context.Request.Path}]: {ex}");
    }
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync("{\"error\":\"Internal server error\"}");
}));

if (!app.Environment.IsDevelopment() && !runningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapGet("/api/runtime-config.js", (HttpContext context) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    var js = $"window.__APP_REGION__ = '{VehiclesController.Region}';";
    if (!string.IsNullOrEmpty(activeCurrency))
        js += $"\nwindow.__APP_CURRENCY__ = '{activeCurrency}';";
    return Results.Content(js, "application/javascript");
});

app.MapGet("/api/setup-status", () =>
{
    var portEnv = Environment.GetEnvironmentVariable("webui_port");
    var currentPort = int.TryParse(portEnv, out var p) ? p : (fileConfig?.Port ?? 2233);
    return Results.Ok(new AppSetupStatusDto
    {
        SetupRequired = setupRequiredCheck(),
        DataDirectoryPath = dataDirectoryPath,
        ConfigFilePath = configFilePath,
        CurrentRegion = VehiclesController.Region,
        CurrentCurrency = activeCurrency,
        CurrentPort = currentPort,
        DebugSavePlaywrightHtml = VehiclesController.DebugSaveHtml,
        IsContainer = runningInContainer,
    });
});

app.MapGet("/api/app-config", () =>
{
    return Results.Ok(new AppConfigurationDto
    {
        Region = VehiclesController.Region,
        DataDirectoryPath = dataDirectoryPath,
        ConfigFilePath = configFilePath,
        DebugSavePlaywrightHtml = VehiclesController.DebugSaveHtml,
    });
});

app.MapPost("/api/app-config", (SaveAppConfigurationDto request) =>
{
    var normalizedRegion = NormalizeRegion(request.Region);
    var normalizedCurrency = string.IsNullOrWhiteSpace(request.Currency)
        ? null
        : request.Currency.Trim().ToUpperInvariant();

    var normalizedPort = (request.Port is int rp && rp >= 1024 && rp <= 65535) ? rp : (int?)null;

    var newConfig = new AppFileConfig
    {
        Region = normalizedRegion,
        Currency = normalizedCurrency,
        Port = normalizedPort,
        Debug = new AppFileDebugConfig
        {
            SavePlaywrightHtml = request.DebugSavePlaywrightHtml,
        },
    };

    File.WriteAllText(configFilePath, JsonSerializer.Serialize(newConfig, new JsonSerializerOptions
    {
        WriteIndented = true,
    }));

    VehiclesController.Region = normalizedRegion;
    VehiclesController.LogFilePath = Path.Combine(dataDirectoryPath, "lookup.log");
    VehiclesController.DebugSaveHtml = request.DebugSavePlaywrightHtml;
    activeCurrency = normalizedCurrency;

    AppLog($"Config saved — Region: {normalizedRegion}  Currency: {normalizedCurrency ?? "(region default)"}  DebugSaveHtml: {request.DebugSavePlaywrightHtml}");

    return Results.Ok(new SaveAppConfigurationResultDto
    {
        Saved = true,
        ConfigFilePath = configFilePath,
    });
});

app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static void InitializeDatabase(CarBudgetDbContext db)
{
    db.Database.EnsureCreated();

    if (db.Database.IsSqlite())
    {
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "VehicleLookupCache" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_VehicleLookupCache" PRIMARY KEY AUTOINCREMENT,
                "LicensePlate" TEXT NOT NULL,
                "Make" TEXT NULL,
                "Model" TEXT NULL,
                "Year" INTEGER NULL,
                "Vin" TEXT NULL,
                "InTraffic" TEXT NULL,
                "SwedishSold" TEXT NULL,
                "ColorName" TEXT NULL,
                "OwnerCount" TEXT NULL,
                "MileageKm" INTEGER NULL,
                "BodyType" TEXT NULL,
                "Classification" TEXT NULL,
                "Generation" TEXT NULL,
                "Engine" TEXT NULL,
                "FuelType" TEXT NULL,
                "Gearbox" TEXT NULL,
                "DriveTrain" TEXT NULL,
                "FuelConsumptionMixed" TEXT NULL,
                "Co2Mixed" TEXT NULL,
                "CargoVolume" TEXT NULL,
                "SeatCount" TEXT NULL,
                "SpecificationsJson" TEXT NULL,
                "RawHtml" TEXT NULL,
                "SourceUrl" TEXT NOT NULL,
                "FetchedAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                CONSTRAINT "CK_VehicleLookupCache_Year_Range" CHECK (Year IS NULL OR (Year >= 1886 AND Year <= 2100)),
                CONSTRAINT "CK_VehicleLookupCache_Mileage_NonNegative" CHECK (MileageKm IS NULL OR MileageKm >= 0)
            );
            """);
        db.Database.ExecuteSqlRaw(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_VehicleLookupCache_LicensePlate" ON "VehicleLookupCache" ("LicensePlate");
            """);
        foreach (var statement in new[]
        {
            "ALTER TABLE \"Vehicles\" ADD COLUMN \"PhotoDataUrl\" TEXT NULL;",
            "ALTER TABLE \"Expenses\" ADD COLUMN \"PhotoDataUrlsJson\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"Vin\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"InTraffic\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"SwedishSold\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"OwnerCount\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"BodyType\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"Classification\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"Generation\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"Engine\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"FuelConsumptionMixed\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"Co2Mixed\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"CargoVolume\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"SeatCount\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"SpecificationsJson\" TEXT NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN \"RawHtml\" TEXT NULL;"
        })
        {
            try { db.Database.ExecuteSqlRaw(statement); } catch { }
        }
    }
    else if (db.Database.IsNpgsql())
    {
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "VehicleLookupCache" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "LicensePlate" character varying(20) NOT NULL,
                "Make" character varying(100) NULL,
                "Model" character varying(100) NULL,
                "Year" integer NULL,
                "Vin" character varying(50) NULL,
                "InTraffic" character varying(20) NULL,
                "SwedishSold" character varying(20) NULL,
                "ColorName" character varying(50) NULL,
                "OwnerCount" character varying(20) NULL,
                "MileageKm" integer NULL,
                "BodyType" character varying(100) NULL,
                "Classification" character varying(150) NULL,
                "Generation" character varying(100) NULL,
                "Engine" character varying(200) NULL,
                "FuelType" character varying(100) NULL,
                "Gearbox" character varying(100) NULL,
                "DriveTrain" character varying(100) NULL,
                "FuelConsumptionMixed" character varying(50) NULL,
                "Co2Mixed" character varying(50) NULL,
                "CargoVolume" character varying(50) NULL,
                "SeatCount" character varying(20) NULL,
                "SpecificationsJson" text NULL,
                "RawHtml" text NULL,
                "SourceUrl" character varying(500) NOT NULL,
                "FetchedAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "CK_VehicleLookupCache_Year_Range" CHECK ("Year" IS NULL OR ("Year" >= 1886 AND "Year" <= 2100)),
                CONSTRAINT "CK_VehicleLookupCache_Mileage_NonNegative" CHECK ("MileageKm" IS NULL OR "MileageKm" >= 0)
            );
            """);
        db.Database.ExecuteSqlRaw(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_VehicleLookupCache_LicensePlate" ON "VehicleLookupCache" ("LicensePlate");
            """);
        foreach (var statement in new[]
        {
            "ALTER TABLE \"Vehicles\" ADD COLUMN IF NOT EXISTS \"PhotoDataUrl\" text NULL;",
            "ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"PhotoDataUrlsJson\" text NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"Vin\" character varying(50) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"InTraffic\" character varying(20) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"SwedishSold\" character varying(20) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"OwnerCount\" character varying(20) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"BodyType\" character varying(100) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"Classification\" character varying(150) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"Generation\" character varying(100) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"Engine\" character varying(200) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"FuelConsumptionMixed\" character varying(50) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"Co2Mixed\" character varying(50) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"CargoVolume\" character varying(50) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"SeatCount\" character varying(20) NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"SpecificationsJson\" text NULL;",
            "ALTER TABLE \"VehicleLookupCache\" ADD COLUMN IF NOT EXISTS \"RawHtml\" text NULL;"
        })
        {
            db.Database.ExecuteSqlRaw(statement);
        }
    }
}

static string ResolveDataDirectory(bool runningInContainer)
{
    var configuredPath = Environment.GetEnvironmentVariable("CARBUDGET_DATA_DIR");
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        return Path.GetFullPath(configuredPath);
    }

    if (runningInContainer)
    {
        return "/app/data";
    }

    return Directory.GetCurrentDirectory();
}

static AppFileConfig? LoadFileConfig(string configFilePath)
{
    try
    {
        if (!File.Exists(configFilePath))
        {
            return null;
        }

        var json = File.ReadAllText(configFilePath);
        return JsonSerializer.Deserialize<AppFileConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
    }
    catch
    {
        return null;
    }
}

sealed class AppFileConfig
{
    public string? Region { get; set; }
    public string? Currency { get; set; }
    public int? Port { get; set; }
    public AppFileDebugConfig? Debug { get; set; }
}

sealed class AppFileDebugConfig
{
    public bool SavePlaywrightHtml { get; set; }
}

sealed class AppSetupStatusDto
{
    public bool SetupRequired { get; set; }
    public string DataDirectoryPath { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    public string CurrentRegion { get; set; } = "sweden";
    public string? CurrentCurrency { get; set; }
    public int CurrentPort { get; set; } = 2233;
    public bool DebugSavePlaywrightHtml { get; set; }
    public bool IsContainer { get; set; }
}

sealed class AppConfigurationDto
{
    public string Region { get; set; } = "sweden";
    public string DataDirectoryPath { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    public bool DebugSavePlaywrightHtml { get; set; }
}

sealed class SaveAppConfigurationDto
{
    public string? Region { get; set; }
    public string? Currency { get; set; }
    public int? Port { get; set; }
    public bool DebugSavePlaywrightHtml { get; set; }
}

sealed class SaveAppConfigurationResultDto
{
    public bool Saved { get; set; }
    public string ConfigFilePath { get; set; } = string.Empty;
}
