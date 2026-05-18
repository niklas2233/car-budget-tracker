using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using CarBudget.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

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

var databaseProvider = builder.Configuration["Database:Provider"]
    ?? Environment.GetEnvironmentVariable("CARBUDGET_DB_PROVIDER")
    ?? "Sqlite";

var defaultSqliteConnectionString = runningInContainer
    ? "Data Source=/app/data/carbudget.db"
    : "Data Source=carbudget.db";

var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection")
    ?? defaultSqliteConnectionString;

if (runningInContainer && string.Equals(sqliteConnectionString.Trim(), "Data Source=carbudget.db", StringComparison.OrdinalIgnoreCase))
{
    sqliteConnectionString = "Data Source=/app/data/carbudget.db";
}

var postgresConnectionString = builder.Configuration.GetConnectionString("PostgresConnection")
    ?? "Host=localhost;Port=5432;Database=carbudget;Username=postgres;Password=postgres";

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

static string NormalizeRegion(string? value)
{
    var region = (value ?? string.Empty).Trim().ToLowerInvariant();
    return region switch
    {
        "norway" => "norway",
        "europe" => "europe",
        _ => "sweden"
    };
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CarBudgetDbContext>();
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
            try
            {
                db.Database.ExecuteSqlRaw(statement);
            }
            catch
            {
            }
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

if (!app.Environment.IsDevelopment() && !runningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapGet("/api/runtime-config.js", (HttpContext context) =>
{
    var region = NormalizeRegion(Environment.GetEnvironmentVariable("region"));
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    return Results.Content($"window.__APP_REGION__ = '{region}';", "application/javascript");
});

app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

