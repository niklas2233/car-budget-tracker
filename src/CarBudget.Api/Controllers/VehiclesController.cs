using CarBudget.Api.DTOs;
using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CarBudget.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    internal static string Region = "sweden";
    internal static string LogFilePath = "";
    internal static bool DebugSaveHtml = false;

    private const string RateLimitedSentinel = "__RATE_LIMITED__";

    private static readonly SemaphoreSlim BrowserInitLock = new(1, 1);
    private static readonly object LogLock = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> InflightLookupFetches = new(StringComparer.OrdinalIgnoreCase);
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    private static void Log(string message)
    {
        if (string.IsNullOrEmpty(LogFilePath)) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        try { lock (LogLock) { System.IO.File.AppendAllText(LogFilePath, line); } }
        catch { }
    }

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly CarBudgetDbContext _dbContext;

    public VehiclesController(IVehicleRepository vehicleRepository, IExpenseRepository expenseRepository, CarBudgetDbContext dbContext)
    {
        _vehicleRepository = vehicleRepository;
        _expenseRepository = expenseRepository;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetAll()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var vehicleDtos = new List<VehicleDto>();

        foreach (var vehicle in vehicles)
        {
            var totalExpenses = await _expenseRepository.GetTotalExpensesByVehicleIdAsync(vehicle.Id);
            vehicleDtos.Add(new VehicleDto
            {
                Id = vehicle.Id,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                PhotoDataUrl = vehicle.PhotoDataUrl,
                VIN = vehicle.VIN,
                LicensePlate = vehicle.LicensePlate,
                PurchasePrice = vehicle.PurchasePrice,
                PurchaseDate = vehicle.PurchaseDate,
                Mileage = vehicle.Mileage,
                Color = vehicle.Color,
                Nickname = vehicle.Nickname,
                SellPrice = vehicle.SellPrice,
                SellDate = vehicle.SellDate,
                TotalExpenses = totalExpenses,
                ExpenseCount = vehicle.Expenses?.Count ?? 0
            });
        }

        return Ok(vehicleDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehicleDto>> GetById(int id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null)
            return NotFound();

        var totalExpenses = await _expenseRepository.GetTotalExpensesByVehicleIdAsync(vehicle.Id);
        var vehicleDto = new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            PhotoDataUrl = vehicle.PhotoDataUrl,
            VIN = vehicle.VIN,
            LicensePlate = vehicle.LicensePlate,
            PurchasePrice = vehicle.PurchasePrice,
            PurchaseDate = vehicle.PurchaseDate,
            Mileage = vehicle.Mileage,
            Color = vehicle.Color,
            Nickname = vehicle.Nickname,
            SellPrice = vehicle.SellPrice,
            SellDate = vehicle.SellDate,
            TotalExpenses = totalExpenses,
            ExpenseCount = vehicle.Expenses?.Count ?? 0
        };

        return Ok(vehicleDto);
    }

    [HttpGet("by-license-plate/{licensePlate}")]
    public async Task<ActionResult<VehicleDto>> GetByLicensePlate(string licensePlate)
    {
        var vehicle = await _vehicleRepository.GetByLicensePlateAsync(licensePlate);
        if (vehicle == null)
            return NotFound();

        var totalExpenses = await _expenseRepository.GetTotalExpensesByVehicleIdAsync(vehicle.Id);
        var vehicleDto = new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            PhotoDataUrl = vehicle.PhotoDataUrl,
            VIN = vehicle.VIN,
            LicensePlate = vehicle.LicensePlate,
            PurchasePrice = vehicle.PurchasePrice,
            PurchaseDate = vehicle.PurchaseDate,
            Mileage = vehicle.Mileage,
            Color = vehicle.Color,
            Nickname = vehicle.Nickname,
            SellPrice = vehicle.SellPrice,
            SellDate = vehicle.SellDate,
            TotalExpenses = totalExpenses,
            ExpenseCount = vehicle.Expenses?.Count ?? 0
        };

        return Ok(vehicleDto);
    }

    [HttpGet("lookup-car-info/{licensePlate}")]
    public async Task<ActionResult<VehicleLookupDto>> LookupCarInfo(string licensePlate)
    {
        return await LookupCarInfoInternal(licensePlate, forceRefresh: false);
    }

    [HttpPost("lookup-car-info/{licensePlate}/refresh")]
    public async Task<ActionResult<VehicleLookupDto>> RefreshCarInfo(string licensePlate)
    {
        return await LookupCarInfoInternal(licensePlate, forceRefresh: true);
    }

    [HttpGet("lookup-cache")]
    public async Task<ActionResult<IEnumerable<VehicleLookupCacheDto>>> GetLookupCache()
    {
        var cacheEntries = await _dbContext.VehicleLookupCaches
            .OrderByDescending(x => x.FetchedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new VehicleLookupCacheDto
            {
                Id = x.Id,
                LicensePlate = x.LicensePlate,
                Make = x.Make,
                Model = x.Model,
                Year = x.Year,
                Vin = x.Vin,
                InTraffic = x.InTraffic,
                SwedishSold = x.SwedishSold,
                ColorName = x.ColorName,
                OwnerCount = x.OwnerCount,
                MileageKm = x.MileageKm,
                BodyType = x.BodyType,
                Classification = x.Classification,
                Generation = x.Generation,
                Engine = x.Engine,
                FuelType = x.FuelType,
                Gearbox = x.Gearbox,
                DriveTrain = x.DriveTrain,
                FuelConsumptionMixed = x.FuelConsumptionMixed,
                Co2Mixed = x.Co2Mixed,
                CargoVolume = x.CargoVolume,
                SeatCount = x.SeatCount,
                Specifications = string.IsNullOrWhiteSpace(x.SpecificationsJson)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(x.SpecificationsJson) ?? new Dictionary<string, string>(),
                SourceUrl = x.SourceUrl,
                CreatedAt = x.CreatedAt,
                FetchedAt = x.FetchedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(cacheEntries);
    }

    private async Task<ActionResult<VehicleLookupDto>> LookupCarInfoInternal(string licensePlate, bool forceRefresh)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            return BadRequest("License plate is required.");

        var normalizedPlate = NormalizePlate(licensePlate);
        if (normalizedPlate.Length < 5)
            return BadRequest("License plate looks invalid.");

        var cachedLookup = await _dbContext.VehicleLookupCaches
            .FirstOrDefaultAsync(x => x.LicensePlate == normalizedPlate);

        var hasRichCachedSpecs = cachedLookup != null &&
            (!string.IsNullOrWhiteSpace(cachedLookup.SpecificationsJson) ||
             !string.IsNullOrWhiteSpace(cachedLookup.RawHtml));

        var hasCachedLookupFields = cachedLookup != null &&
            cachedLookup.MileageKm != null &&
            !string.IsNullOrWhiteSpace(cachedLookup.ColorName);

        if (hasRichCachedSpecs && hasCachedLookupFields && !forceRefresh)
        {
            return Ok(MapCacheToDto(cachedLookup!));
        }

        var url = Region == "norway"
            ? $"https://www.car.info/en-no/license-plate/N/{normalizedPlate}#specs"
            : $"https://www.car.info/en-se/license-plate/S/{normalizedPlate}#specs";

        Log($"LOOKUP START  plate={normalizedPlate}  region={Region}  forceRefresh={forceRefresh}  url={url}");

        var parseHtml = await FetchRenderedPageHtmlDeduplicatedAsync(url, normalizedPlate, forceRefresh);
        if (parseHtml == RateLimitedSentinel)
        {
            Log($"LOOKUP FAILED  plate={normalizedPlate}  reason=Rate limited by car.info");
            if (!forceRefresh && hasCachedLookupFields)
                return Ok(MapCacheToDto(cachedLookup!));

            return StatusCode(429, "Rate limited by car.info. Wait a minute and try again.");
        }

        if (string.IsNullOrWhiteSpace(parseHtml))
        {
            Log($"LOOKUP FAILED  plate={normalizedPlate}  reason=Playwright returned null/empty HTML");
            if (!forceRefresh && hasCachedLookupFields)
                return Ok(MapCacheToDto(cachedLookup!));

            return StatusCode(503, "Could not render Car.info with Playwright. Try again in a minute.");
        }

        Log($"LOOKUP HTML   plate={normalizedPlate}  htmlBytes={parseHtml.Length}");

        var dto = await ParseCarInfoLookup(parseHtml, normalizedPlate, url);

        Log($"LOOKUP PARSE  plate={normalizedPlate}  make={dto.Make}  model={dto.Model}  year={dto.Year}  color={dto.ColorName}  gearbox={dto.Gearbox}  fuel={dto.FuelType}  mileageKm={dto.MileageKm}  specsCount={dto.Specifications.Count}");

        if (dto.Make == null && dto.Model == null && dto.Year == null)
        {
            Log($"LOOKUP FAILED  plate={normalizedPlate}  reason=Could not extract make/model/year from HTML");
            return NotFound("Could not extract vehicle data from Car.info page.");
        }

        var cacheEntry = cachedLookup ?? new VehicleLookupCache
        {
            LicensePlate = dto.LicensePlate,
            CreatedAt = DateTime.UtcNow,
        };

        cacheEntry.Make = dto.Make;
        cacheEntry.Model = dto.Model;
        cacheEntry.Year = dto.Year;
        cacheEntry.Vin = dto.Vin;
        cacheEntry.InTraffic = dto.InTraffic;
        cacheEntry.SwedishSold = dto.SwedishSold;
        cacheEntry.ColorName = dto.ColorName;
        cacheEntry.OwnerCount = dto.OwnerCount;
        cacheEntry.MileageKm = dto.MileageKm;
        cacheEntry.BodyType = dto.BodyType;
        cacheEntry.Classification = dto.Classification;
        cacheEntry.Generation = dto.Generation;
        cacheEntry.Engine = dto.Engine;
        cacheEntry.FuelType = dto.FuelType;
        cacheEntry.Gearbox = dto.Gearbox;
        cacheEntry.DriveTrain = dto.DriveTrain;
        cacheEntry.FuelConsumptionMixed = dto.FuelConsumptionMixed;
        cacheEntry.Co2Mixed = dto.Co2Mixed;
        cacheEntry.CargoVolume = dto.CargoVolume;
        cacheEntry.SeatCount = dto.SeatCount;
        cacheEntry.SpecificationsJson = JsonSerializer.Serialize(dto.Specifications);
        cacheEntry.RawHtml = parseHtml;
        cacheEntry.SourceUrl = dto.SourceUrl;
        cacheEntry.FetchedAt = DateTime.UtcNow;
        cacheEntry.UpdatedAt = DateTime.UtcNow;

        if (cachedLookup == null)
        {
            _dbContext.VehicleLookupCaches.Add(cacheEntry);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create(CreateVehicleDto dto)
    {
        var vehicle = new Vehicle
        {
            Make = dto.Make,
            Model = dto.Model,
            Year = dto.Year,
            PhotoDataUrl = dto.PhotoDataUrl,
            VIN = dto.VIN,
            LicensePlate = dto.LicensePlate,
            PurchasePrice = dto.PurchasePrice,
            PurchaseDate = dto.PurchaseDate,
            Mileage = dto.Mileage,
            Color = dto.Color,
            Nickname = dto.Nickname,
            SellPrice = dto.SellPrice,
            SellDate = dto.SellDate
        };

        try
        {
            await _vehicleRepository.AddAsync(vehicle);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict("A vehicle with the same VIN or license plate already exists.");
        }

        var vehicleDto = new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            PhotoDataUrl = vehicle.PhotoDataUrl,
            VIN = vehicle.VIN,
            LicensePlate = vehicle.LicensePlate,
            PurchasePrice = vehicle.PurchasePrice,
            PurchaseDate = vehicle.PurchaseDate,
            Mileage = vehicle.Mileage,
            Color = vehicle.Color,
            Nickname = vehicle.Nickname,
            SellPrice = vehicle.SellPrice,
            SellDate = vehicle.SellDate,
            TotalExpenses = 0,
            ExpenseCount = 0
        };

        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicleDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateVehicleDto dto)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null)
            return NotFound();

        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.Year = dto.Year;
        vehicle.PhotoDataUrl = dto.PhotoDataUrl;
        vehicle.VIN = dto.VIN;
        vehicle.LicensePlate = dto.LicensePlate;
        vehicle.PurchasePrice = dto.PurchasePrice;
        vehicle.PurchaseDate = dto.PurchaseDate;
        vehicle.Mileage = dto.Mileage;
        vehicle.Color = dto.Color;
        vehicle.Nickname = dto.Nickname;
        vehicle.SellPrice = dto.SellPrice;
        vehicle.SellDate = dto.SellDate;

        try
        {
            await _vehicleRepository.UpdateAsync(vehicle);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict("A vehicle with the same VIN or license plate already exists.");
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null)
            return NotFound();

        await _vehicleRepository.DeleteAsync(id);

        return NoContent();
    }

    [HttpGet("{id}/export")]
    public async Task<ActionResult<VehicleExportPackageDto>> ExportVehicle(int id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null)
            return NotFound();

        var package = new VehicleExportPackageDto
        {
            Vehicle = new VehicleExportDto
            {
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                PhotoDataUrl = vehicle.PhotoDataUrl,
                VIN = vehicle.VIN,
                LicensePlate = vehicle.LicensePlate,
                PurchasePrice = vehicle.PurchasePrice,
                PurchaseDate = vehicle.PurchaseDate,
                Mileage = vehicle.Mileage,
                Color = vehicle.Color,
                Nickname = vehicle.Nickname,
                SellPrice = vehicle.SellPrice,
                SellDate = vehicle.SellDate,
            },
            Expenses = vehicle.Expenses
                .OrderBy(e => e.Date)
                .Select(e => new ExpenseExportDto
                {
                    Type = e.Type,
                    Description = e.Description,
                    PhotoDataUrls = string.IsNullOrWhiteSpace(e.PhotoDataUrlsJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(e.PhotoDataUrlsJson) ?? new List<string>(),
                    Amount = e.Amount,
                    Date = e.Date,
                    Mileage = e.Mileage,
                    Vendor = e.Vendor,
                    Notes = e.Notes,
                    Shipping = e.Shipping,
                })
                .ToList()
        };

        return Ok(package);
    }

    [HttpPost("import")]
    public async Task<ActionResult<VehicleDto>> ImportVehicle([FromBody] VehicleExportPackageDto dto, [FromQuery] bool overwrite = false)
    {
        if (dto.Vehicle == null)
            return BadRequest("Vehicle payload is required.");

        var normalizedVin = string.IsNullOrWhiteSpace(dto.Vehicle.VIN) ? null : dto.Vehicle.VIN.Trim().ToUpperInvariant();
        var normalizedLicensePlate = string.IsNullOrWhiteSpace(dto.Vehicle.LicensePlate) ? null : dto.Vehicle.LicensePlate.Trim().ToUpperInvariant();

        if (overwrite)
        {
            var idsToDelete = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(normalizedVin))
            {
                var existing = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.VIN == normalizedVin);
                if (existing != null) idsToDelete.Add(existing.Id);
            }
            if (!string.IsNullOrWhiteSpace(normalizedLicensePlate))
            {
                var existing = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == normalizedLicensePlate);
                if (existing != null) idsToDelete.Add(existing.Id);
            }
            foreach (var id in idsToDelete)
                await _vehicleRepository.DeleteAsync(id);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(normalizedVin))
            {
                var existingByVin = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.VIN == normalizedVin);
                if (existingByVin != null)
                    return Conflict("A vehicle with the same VIN already exists.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedLicensePlate))
            {
                var existingByPlate = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == normalizedLicensePlate);
                if (existingByPlate != null)
                    return Conflict("A vehicle with the same license plate already exists.");
            }
        }

        var vehicle = new Vehicle
        {
            Make = dto.Vehicle.Make,
            Model = dto.Vehicle.Model,
            Year = dto.Vehicle.Year,
            PhotoDataUrl = dto.Vehicle.PhotoDataUrl,
            VIN = dto.Vehicle.VIN,
            LicensePlate = dto.Vehicle.LicensePlate,
            PurchasePrice = dto.Vehicle.PurchasePrice,
            PurchaseDate = dto.Vehicle.PurchaseDate,
            Mileage = dto.Vehicle.Mileage,
            Color = dto.Vehicle.Color,
            Nickname = dto.Vehicle.Nickname,
            SellPrice = dto.Vehicle.SellPrice,
            SellDate = dto.Vehicle.SellDate,
        };

        try
        {
            await _vehicleRepository.AddAsync(vehicle);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict("A vehicle with the same VIN or license plate already exists.");
        }

        foreach (var importedExpense in dto.Expenses ?? new List<ExpenseExportDto>())
        {
            _dbContext.Expenses.Add(new Expense
            {
                VehicleId = vehicle.Id,
                Type = importedExpense.Type,
                Description = importedExpense.Description,
                PhotoDataUrlsJson = importedExpense.PhotoDataUrls.Count == 0 ? null : JsonSerializer.Serialize(importedExpense.PhotoDataUrls),
                Amount = importedExpense.Amount,
                Date = importedExpense.Date,
                Mileage = importedExpense.Mileage,
                Vendor = importedExpense.Vendor,
                Notes = importedExpense.Notes,
                Shipping = importedExpense.Shipping,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _dbContext.SaveChangesAsync();

        var totalExpenses = await _expenseRepository.GetTotalExpensesByVehicleIdAsync(vehicle.Id);

        var vehicleDto = new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            PhotoDataUrl = vehicle.PhotoDataUrl,
            VIN = vehicle.VIN,
            LicensePlate = vehicle.LicensePlate,
            PurchasePrice = vehicle.PurchasePrice,
            PurchaseDate = vehicle.PurchaseDate,
            Mileage = vehicle.Mileage,
            Color = vehicle.Color,
            Nickname = vehicle.Nickname,
            SellPrice = vehicle.SellPrice,
            SellDate = vehicle.SellDate,
            TotalExpenses = totalExpenses,
            ExpenseCount = dto.Expenses?.Count ?? 0
        };

        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicleDto);
    }

    private static async Task<VehicleLookupDto> ParseCarInfoLookup(string html, string normalizedPlate, string sourceUrl)
    {
        var result = new VehicleLookupDto
        {
            LicensePlate = normalizedPlate,
            SourceUrl = sourceUrl
        };

        foreach (Match match in Regex.Matches(html, "<script[^>]*type=\"application/ld\\+json\"[^>]*>([\\s\\S]*?)</script>", RegexOptions.IgnoreCase))
        {
            var json = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("mainEntity", out var mainEntity))
                    continue;

                if (mainEntity.TryGetProperty("brand", out var brand) && brand.TryGetProperty("name", out var brandName))
                    result.Make = brandName.GetString();

                if (result.Make == null && mainEntity.TryGetProperty("manufacturer", out var manufacturer))
                    result.Make = manufacturer.GetString();

                if (mainEntity.TryGetProperty("vehicleModelDate", out var yearNode) && int.TryParse(yearNode.GetString(), out var year))
                    result.Year = year;

                if (mainEntity.TryGetProperty("color", out var colorNode))
                    result.ColorName = colorNode.GetString();

                if (mainEntity.TryGetProperty("description", out var descriptionNode))
                {
                    result.Specifications["description"] = descriptionNode.GetString() ?? string.Empty;
                }

                if (mainEntity.TryGetProperty("vehicleEngine", out var engineNode) && engineNode.TryGetProperty("fuelType", out var fuelNode))
                    result.FuelType = fuelNode.GetString();

                if (mainEntity.TryGetProperty("mileageFromOdometer", out var mileageNode) && mileageNode.TryGetProperty("value", out var mileageValue))
                {
                    var mileageText = mileageValue.GetString();
                    // Value may be "105,000 km" or "10500 mil" — capture digits, spaces and comma/dot separators
                    var mileageMatch = mileageText == null ? null : Regex.Match(mileageText, @"([0-9][0-9\s,\.]*)");
                    if (mileageMatch != null && mileageMatch.Success)
                    {
                        var digits = mileageMatch.Groups[1].Value.Replace(" ", "").Replace(",", "").Replace(".", "").TrimEnd();
                        if (int.TryParse(digits, out var rawValue))
                        {
                            // Default: assume value is in km. Only multiply ×10 when we can confirm
                            // the unit is Scandinavian "mil" (1 mil = 10 km).
                            string? unitCode = null, unitText = null;
                            if (mileageNode.TryGetProperty("unitCode", out var unitCodeNode))
                                unitCode = unitCodeNode.GetString();
                            if (mileageNode.TryGetProperty("unitText", out var unitTextNode))
                                unitText = unitTextNode.GetString();

                            var isMil =
                                string.Equals(unitCode, "SMI", StringComparison.OrdinalIgnoreCase)
                                || (unitText != null && unitText.Trim().Equals("mil", StringComparison.OrdinalIgnoreCase))
                                || (mileageText != null && Regex.IsMatch(mileageText, @"\bmil\b", RegexOptions.IgnoreCase));

                            result.MileageKm = isMil ? rawValue * 10 : rawValue;
                            Log($"MILEAGE JSONLD  raw={rawValue}  unitCode={unitCode ?? "(none)"}  unitText={unitText ?? "(none)"}  valueText={mileageText}  isMil={isMil}  result={result.MileageKm}");
                        }
                    }
                }

                if (root.TryGetProperty("breadcrumb", out var breadcrumb) &&
                    breadcrumb.TryGetProperty("itemListElement", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty("position", out var positionNode))
                            continue;

                        int position = -1;
                        if (positionNode.ValueKind == JsonValueKind.Number)
                            positionNode.TryGetInt32(out position);
                        else if (positionNode.ValueKind == JsonValueKind.String)
                            int.TryParse(positionNode.GetString(), out position);

                        if (position != 3)
                            continue;

                        if (item.TryGetProperty("item", out var nestedItem) && nestedItem.TryGetProperty("name", out var modelNode))
                        {
                            result.Model = modelNode.GetString();
                            break;
                        }
                    }
                }

                if (result.Model == null && mainEntity.TryGetProperty("name", out var nameNode))
                {
                    var fullName = nameNode.GetString();
                    if (!string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(result.Make))
                    {
                        var trimmed = fullName.StartsWith(result.Make + " ", StringComparison.OrdinalIgnoreCase)
                            ? fullName[(result.Make.Length + 1)..]
                            : fullName;

                        result.Model = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    }
                }

                break;
            }
            catch (JsonException)
            {
                // Ignore invalid JSON-LD blocks and continue.
            }
        }

        result.Specifications = ExtractSpecs(html);
        var specs = result.Specifications;

        // Make/model/year from specs (Norwegian labels) if JSON-LD didn't deliver them.
        if (result.Make == null)
        {
            var makeRaw = GetSpecAny(specs, "Merke", "Fabrikat", "Märke");
            if (!string.IsNullOrWhiteSpace(makeRaw))
                result.Make = makeRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        if (result.Model == null)
        {
            var modelRaw = GetSpecAny(specs, "Modell");
            if (!string.IsNullOrWhiteSpace(modelRaw))
                result.Model = modelRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        if (result.Year == null)
        {
            var yearRaw = GetSpecAny(specs, "Årsmodell", "Modellår", "Årgång", "Modellår");
            if (!string.IsNullOrWhiteSpace(yearRaw))
            {
                var yearMatch = Regex.Match(yearRaw, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var parsedYear))
                    result.Year = parsedYear;
            }
        }

        // Final fallback: parse make/model/year from the page <title> tag.
        // Title formats seen:
        //   "DP83970 - Volkswagen Passat Variant GTE 1.4 TSI ACT DSG Sekventiell, 218hk, 2016"
        //   "Volkswagen Golf 2018 | car.info"
        if (result.Make == null || result.Model == null || result.Year == null)
        {
            var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                var titleText = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                Log($"PARSE TITLE   \"{titleText}\"");

                // Split on separators (pipe, dash, en-dash), then pick the first segment that
                // looks like car info: more than 8 chars and not a bare plate number.
                var segments = Regex.Split(titleText, @"\s*[|–]\s*|\s+-\s+");
                var carSegment = segments
                    .Select(s => s.Trim())
                    .FirstOrDefault(s =>
                        s.Length > 8 &&
                        !Regex.IsMatch(s, @"^[A-Z0-9]{4,10}$", RegexOptions.IgnoreCase) &&
                        !s.Contains("car.info", StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;

                Log($"PARSE TITLE   car segment: \"{carSegment}\"");

                if (!string.IsNullOrWhiteSpace(carSegment))
                {
                    var words = carSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2)
                    {
                        result.Make ??= words[0];
                        result.Model ??= words[1];
                    }
                    if (result.Year == null)
                    {
                        var yearInTitle = Regex.Match(carSegment, @"\b(19|20)\d{2}\b");
                        if (yearInTitle.Success && int.TryParse(yearInTitle.Value, out var ty))
                            result.Year = ty;
                    }
                }
            }
        }

        result.InTraffic ??= GetSpecAny(specs, "I trafik", "I trafikk", "In traffic");
        result.SwedishSold ??= GetSpecAny(specs, "Svensksåld", "Norsksolgt", "Norsk-solgt", "Sold in Sweden", "Sold in Norway");
        result.ColorName ??= GetSpecAny(specs, "Färg", "Farge", "Color", "Colour");
        result.OwnerCount ??= GetSpecAny(specs, "Antal ägare", "Antall eiere", "Owners", "Number of owners");
        result.Vin ??= GetSpecAny(specs, "Chassinummer (vin)", "Chassisnummer (vin)", "VIN", "Chassis number (VIN)", "Chassis number");
        result.BodyType ??= GetSpecAny(specs, "Kaross", "Karosseri", "Body type", "Body");
        result.Classification ??= GetSpecAny(specs, "Klassificering", "Klassifisering", "Classification");
        result.Generation ??= GetSpecAny(specs, "Generation");
        result.Engine ??= GetSpecAny(specs, "Motor", "Engine");
        result.Gearbox ??= GetSpecAny(specs, "Växellåda", "Girkasse", "Girboks", "Transmission", "Gearbox");
        result.DriveTrain ??= GetSpecAny(specs, "Drivlina", "Drivlinje", "Drivsystem", "Drivning", "Drivhjul", "Drive train", "Drivetrain");
        result.FuelConsumptionMixed ??= GetSpecAny(specs, "Blandad förbrukning", "Blandet forbruk", "Kombinert forbruk", "Blandad", "Mixed consumption", "Combined consumption");
        result.Co2Mixed ??= GetSpecAny(specs, "CO₂, Blandad", "CO₂, blandet", "CO2, blandet", "CO₂, Mixed", "CO2, Mixed");
        result.CargoVolume ??= GetSpecAny(specs, "Bagagevolym", "Bagasjevolum", "Cargo volume", "Trunk volume", "Boot volume");
        result.SeatCount ??= GetSpecAny(specs, "Antal sittplatser", "Antall sitteplasser", "Seats", "Seat count", "Number of seats");
        result.FuelType ??= GetSpecAny(specs, "Bränsle", "Drivstoff", "Fuel", "Fuel type") ?? result.FuelType;

        var plainText = CleanHtmlText(html);

        result.ColorName ??= ExtractFact(plainText, "Färg", "Antal ägare")
            ?? ExtractFact(plainText, "Farge", "Antall eiere")
            ?? ExtractFact(plainText, "Color", "Owners")
            ?? ExtractFact(plainText, "Colour", "Owners");

        if (result.MileageKm == null)
        {
            var mileage = GetSpecAny(specs, "Mätarställning", "Kilometerstand", "Mileage", "Odometer")
                ?? ExtractFact(plainText, "Mätarställning", "Rapporterade mätarställningar")
                ?? ExtractFact(plainText, "Kilometerstand", "Rapporterte")
                ?? ExtractFact(plainText, "Mileage", "Reported mileages")
                ?? ExtractFact(plainText, "Odometer", "Reported");
            if (!string.IsNullOrWhiteSpace(mileage))
            {
                Log($"MILEAGE SPEC    raw=\"{mileage}\"");
                var mileageKm = ParseMileageFromText(mileage);
                Log($"MILEAGE SPEC    parsed={mileageKm?.ToString() ?? "(null)"}");
                if (mileageKm.HasValue)
                {
                    result.MileageKm = mileageKm;
                }
            }
        }

        result.Gearbox ??= ExtractFactByNextLabels(
            plainText,
            "Växellåda",
            "Drivlina",
            "Drivning",
            "Drivhjul",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utrustningsnivå",
            "Blandad förbrukning",
            "CO₂, Blandad",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.Gearbox ??= ExtractFactByNextLabels(
            plainText,
            "Girkasse",
            "Drivlinje",
            "Drivsystem",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utstyrsnivå",
            "Blandet forbruk",
            "CO₂, blandet",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.DriveTrain ??= ExtractFactByNextLabels(
            plainText,
            "Drivlina",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utrustningsnivå",
            "Blandad förbrukning",
            "CO₂, Blandad",
            "Tankvolym",
            "Bagagevolym",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.DriveTrain ??= ExtractFactByNextLabels(
            plainText,
            "Drivning",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utrustningsnivå",
            "Blandad förbrukning",
            "CO₂, Blandad",
            "Tankvolym",
            "Bagagevolym",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.DriveTrain ??= ExtractFactByNextLabels(
            plainText,
            "Drivhjul",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utrustningsnivå",
            "Blandad förbrukning",
            "CO₂, Blandad",
            "Tankvolym",
            "Bagagevolym",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.DriveTrain ??= ExtractFactByNextLabels(
            plainText,
            "Drivlinje",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utstyrsnivå",
            "Blandet forbruk",
            "CO₂, blandet",
            "Tankvolym",
            "Bagasjevolum",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        result.DriveTrain ??= ExtractFactByNextLabels(
            plainText,
            "Drivsystem",
            "Drive train",
            "Body type",
            "Acceleration",
            "Utstyrsnivå",
            "Blandet forbruk",
            "CO₂, blandet",
            "Tankvolym",
            "Bagasjevolum",
            "Fetched",
            "Updated",
            "Source",
            "All captured specs");

        return result;
    }

    private static string NormalizePlate(string plate)
    {
        return Regex.Replace(plate.ToUpperInvariant(), "[^A-Z0-9]", "");
    }

    private static VehicleLookupDto MapCacheToDto(VehicleLookupCache cache)
    {
        var specifications = string.IsNullOrWhiteSpace(cache.SpecificationsJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(cache.SpecificationsJson) ?? new Dictionary<string, string>();

        return new VehicleLookupDto
        {
            LicensePlate = cache.LicensePlate,
            Make = cache.Make,
            Model = cache.Model,
            Year = cache.Year,
            Vin = cache.Vin,
            InTraffic = cache.InTraffic,
            SwedishSold = cache.SwedishSold,
            ColorName = cache.ColorName,
            OwnerCount = cache.OwnerCount,
            MileageKm = cache.MileageKm,
            BodyType = cache.BodyType,
            Classification = cache.Classification,
            Generation = cache.Generation,
            Engine = cache.Engine,
            FuelType = cache.FuelType,
            Gearbox = cache.Gearbox,
            DriveTrain = cache.DriveTrain,
            FuelConsumptionMixed = cache.FuelConsumptionMixed,
            Co2Mixed = cache.Co2Mixed,
            CargoVolume = cache.CargoVolume,
            SeatCount = cache.SeatCount,
            Specifications = specifications,
            SourceUrl = cache.SourceUrl,
        };
    }

    private static Dictionary<string, string> ExtractSpecs(string html)
    {
        var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static bool IsNoisySpecValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            // Drop obvious page-dump contamination that is not a real spec value.
            if (value.Length > 300)
                return true;

            var contaminationMarkers = new[]
            {
                "Läs mer och beställ",
                "Visa alla",
                "Dölj alla",
                "Information markerat i blått",
                "All captured specs",
                "Topplista",
                "Utrustningslista",
                "Fordonsskatt",
                "Besökstrend"
            };

            return contaminationMarkers.Any(marker =>
                value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        static void TryAddSpec(Dictionary<string, string> target, string labelRaw, string valueRaw)
        {
            var label = CleanHtmlText(labelRaw);
            var value = CleanHtmlText(valueRaw);

            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
                return;

            if (label.Length > 120)
                return;

            if (IsNoisySpecValue(value))
                return;

            if (!target.ContainsKey(label))
                target[label] = value;
        }

        var itemMatches = Regex.Matches(
            html,
            "<div[^>]*>\\s*<div[^>]*>(?<label>[^<]+)</div>\\s*<div[^>]*>(?<value>[\\s\\S]*?)</div>\\s*</div>",
            RegexOptions.IgnoreCase);

        foreach (Match match in itemMatches)
        {
            TryAddSpec(specs, match.Groups["label"].Value, match.Groups["value"].Value);
        }

        // Car.info renders most detailed attributes as rows like:
        // <div class="sprow"><span class="sptitle">Label</span><span class="ast-i">Value</span></div>
        var specRowMatches = Regex.Matches(
            html,
            "<div[^>]*class=[\"'][^\"']*sprow[^\"']*[\"'][^>]*>\\s*<span[^>]*class=[\"'][^\"']*sptitle[^\"']*[\"'][^>]*>(?<label>[\\s\\S]*?)</span>\\s*<span[^>]*class=[\"'][^\"']*ast-i[^\"']*[\"'][^>]*>(?<value>[\\s\\S]*?)</span>",
            RegexOptions.IgnoreCase);

        foreach (Match match in specRowMatches)
        {
            TryAddSpec(specs, match.Groups["label"].Value, match.Groups["value"].Value);
        }

        // Car.info also renders rows where the value is plain text directly in the row
        // after the sptitle span, without a dedicated value span.
        var plainSpecRowMatches = Regex.Matches(
            html,
            "<div[^>]*class=[\"'][^\"']*sprow[^\"']*[\"'][^>]*>(?<content>[\\s\\S]*?)</div>",
            RegexOptions.IgnoreCase);

        foreach (Match match in plainSpecRowMatches)
        {
            var content = match.Groups["content"].Value;
            var labelMatch = Regex.Match(
                content,
                "<span[^>]*class=[\"'][^\"']*sptitle[^\"']*[\"'][^>]*>(?<label>[\\s\\S]*?)</span>",
                RegexOptions.IgnoreCase);

            if (!labelMatch.Success)
                continue;

            var labelRaw = labelMatch.Groups["label"].Value;
            var valueRaw = content.Replace(labelMatch.Value, " ", StringComparison.Ordinal);
            TryAddSpec(specs, labelRaw, valueRaw);
        }

        return specs;
    }

    private static Task<string?> FetchRenderedPageHtmlDeduplicatedAsync(string url, string normalizedPlate, bool forceRefresh)
    {
        if (forceRefresh)
            return TryFetchRenderedPageHtmlAsync(url, normalizedPlate);

        return InflightLookupFetches.GetOrAdd(normalizedPlate, _ => FetchAndReleaseAsync(normalizedPlate, url));
    }

    private static async Task<string?> FetchAndReleaseAsync(string normalizedPlate, string url)
    {
        try
        {
            return await TryFetchRenderedPageHtmlAsync(url, normalizedPlate);
        }
        finally
        {
            InflightLookupFetches.TryRemove(normalizedPlate, out _);
        }
    }

    private static async Task<IBrowser> GetOrCreateBrowserAsync()
    {
        if (_browser is { IsConnected: true })
            return _browser;

        await BrowserInitLock.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true })
                return _browser;

            _playwright ??= await Playwright.CreateAsync();

            var browserPath = GetEdgeExecutablePath();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled"
                }
            };

            if (!string.IsNullOrEmpty(browserPath))
                launchOptions.ExecutablePath = browserPath;

            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
            return _browser;
        }
        finally
        {
            BrowserInitLock.Release();
        }
    }

    private static void DumpDebugHtml(string normalizedPlate, string url, string html)
    {
        if (string.IsNullOrEmpty(LogFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath)!;
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var platePart = string.IsNullOrEmpty(normalizedPlate) ? "unknown" : normalizedPlate;
            var file = Path.Combine(dir, $"lookup_debug_{platePart}_{ts}.html");
            lock (LogLock) { System.IO.File.WriteAllText(file, $"<!-- URL: {url} -->\n{html}"); }
            Log($"PLAYWRIGHT    debug HTML saved to {file}");
        }
        catch { }
    }

    private static void DumpFailedHtml(string url, string? html)
    {
        if (string.IsNullOrEmpty(LogFilePath) || string.IsNullOrEmpty(html)) return;
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath)!;
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var file = Path.Combine(dir, $"lookup_failed_{ts}.html");
            lock (LogLock) { System.IO.File.WriteAllText(file, $"<!-- URL: {url} -->\n{html}"); }
            Log($"PLAYWRIGHT    failed HTML saved to {file}");
        }
        catch { }
    }

    private static bool IsRateLimited(string? html)
    {
        if (string.IsNullOrEmpty(html)) return false;
        return html.Contains("coffee", StringComparison.OrdinalIgnoreCase)
            || html.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || html.Contains("429", StringComparison.Ordinal);
    }

    private static async Task<string?> TryFetchRenderedPageHtmlAsync(string url, string normalizedPlate = "")
    {
        try
        {
            Log($"PLAYWRIGHT    navigating to {url}");
            var browser = await GetOrCreateBrowserAsync();

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "en-US",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1600, Height = 2400 }
            });

            var page = await context.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            Log($"PLAYWRIGHT    DOM loaded, waiting for #specifications selector");

            // Inner try so we can read the page HTML on timeout to detect rate-limiting.
            bool specsFound = true;
            try
            {
                await page.WaitForSelectorAsync("#specifications .sprow, #specifications span.sptitle", new PageWaitForSelectorOptions
                {
                    Timeout = 6000
                });
            }
            catch (TimeoutException)
            {
                specsFound = false;
            }

            if (!specsFound)
            {
                var timeoutHtml = await page.ContentAsync();
                DumpFailedHtml(url, timeoutHtml);
                if (IsRateLimited(timeoutHtml))
                {
                    Log("PLAYWRIGHT    RATE LIMITED - coffee break page detected");
                    return RateLimitedSentinel;
                }
                Log("PLAYWRIGHT    TIMEOUT: #specifications selector not found after 6s");
                return null;
            }

            Log($"PLAYWRIGHT    #specifications selector found");

            foreach (var expandLabel in new[] { "Visa fler", "Vis fler", "Vis mer", "Show more" })
            {
                var expandButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = expandLabel });
                if (await expandButton.CountAsync() > 0 && await expandButton.First.IsVisibleAsync())
                {
                    Log($"PLAYWRIGHT    clicking expand button \"{expandLabel}\"");
                    await expandButton.First.ClickAsync();
                    await page.WaitForTimeoutAsync(1200);
                    break;
                }
            }

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 3000 });
            var html = await page.ContentAsync();
            Log($"PLAYWRIGHT    content captured  bytes={html?.Length ?? 0}");
            if (DebugSaveHtml && !string.IsNullOrEmpty(html))
                DumpDebugHtml(normalizedPlate, url, html);
            return html;
        }
        catch (TimeoutException tex)
        {
            Log($"PLAYWRIGHT    TIMEOUT: {tex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log($"PLAYWRIGHT    ERROR: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string? GetEdgeExecutablePath()
    {
        // Windows paths for Edge
        var windowsCandidates = new[]
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        };

        foreach (var candidate in windowsCandidates)
        {
            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        // Linux/Docker paths for Chromium (real binary only, not snap wrappers)
        var linuxCandidates = new[]
        {
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser"
        };

        foreach (var candidate in linuxCandidates)
        {
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Return null to let Playwright use its own bundled Chromium (installed via PLAYWRIGHT_BROWSERS_PATH)
        return null;
    }

    private static string CleanHtmlText(string value)
    {
        value = Regex.Replace(value, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var text = Regex.Replace(value, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        return text;
    }

    private static string? GetSpec(Dictionary<string, string> specs, string label)
    {
        return specs.TryGetValue(label, out var value) ? value : null;
    }

    private static string? GetSpecAny(Dictionary<string, string> specs, params string[] labels)
    {
        foreach (var label in labels)
        {
            if (specs.TryGetValue(label, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ExtractFact(string text, string startLabel, string endLabel)
    {
        var start = text.IndexOf(startLabel, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += startLabel.Length;
        var end = text.IndexOf(endLabel, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = Math.Min(text.Length, start + 120);

        var value = text[start..end].Trim().Trim('-', ':');
        return SanitizeExtractedFact(value);
    }

    private static string? ExtractFactByNextLabels(string text, string startLabel, params string[] possibleEndLabels)
    {
        var start = text.IndexOf(startLabel, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += startLabel.Length;
        var end = text.Length;

        foreach (var endLabel in possibleEndLabels)
        {
            var candidate = text.IndexOf(endLabel, start, StringComparison.OrdinalIgnoreCase);
            if (candidate >= 0 && candidate < end)
                end = candidate;
        }

        if (end == text.Length)
            end = Math.Min(text.Length, start + 120);

        var value = text[start..end].Trim().Trim('-', ':');
        return SanitizeExtractedFact(value);
    }

    private static string? SanitizeExtractedFact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();

        // Drop known JSON-LD/script contamination patterns from plain-text fallback extraction.
        var contaminationMarkers = new[]
        {
            "\"@type\"",
            "\"inLanguage\"",
            "\"itemListElement\"",
            "\"mainEntity\"",
            "\"url\"",
            "https://",
            "http://"
        };

        if (contaminationMarkers.Any(marker => cleaned.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            return null;

        if (cleaned.Length > 120)
            return null;

        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static int? ParseMileageFromText(string text)
    {
        // Swedish/Norwegian "mil" (1 mil = 10 km) — must not match "miles"
        var milMatch = Regex.Match(text, @"([0-9][0-9\s]*)\s*\bmil\b", RegexOptions.IgnoreCase);
        if (milMatch.Success)
        {
            var digits = milMatch.Groups[1].Value.Replace(" ", "");
            if (int.TryParse(digits, out var mileageMil))
                return mileageMil * 10;
        }

        // English/metric km — value is already in kilometres
        var kmMatch = Regex.Match(text, @"([0-9][0-9\s,]*)\s*km", RegexOptions.IgnoreCase);
        if (kmMatch.Success)
        {
            var digits = kmMatch.Groups[1].Value.Replace(" ", "").Replace(",", "");
            if (int.TryParse(digits, out var mileageKm))
                return mileageKm;
        }

        return null;
    }
}
