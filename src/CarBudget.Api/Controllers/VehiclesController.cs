using CarBudget.Api.DTOs;
using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CarBudget.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
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

        var url = $"https://www.car.info/sv-se/license-plate/S/{normalizedPlate}#specs";
        var parseHtml = await TryFetchRenderedPageHtmlAsync(url);
        if (string.IsNullOrWhiteSpace(parseHtml))
        {
            if (!forceRefresh && hasCachedLookupFields)
                return Ok(MapCacheToDto(cachedLookup!));

            return StatusCode(503, "Could not render Car.info with Playwright. Try again in a minute.");
        }

        var dto = await ParseCarInfoLookup(parseHtml, normalizedPlate, url);

        if (dto.Make == null && dto.Model == null && dto.Year == null)
            return NotFound("Could not extract vehicle data from Car.info page.");

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
    public async Task<ActionResult<VehicleDto>> ImportVehicle([FromBody] VehicleExportPackageDto dto)
    {
        if (dto.Vehicle == null)
            return BadRequest("Vehicle payload is required.");

        var normalizedVin = string.IsNullOrWhiteSpace(dto.Vehicle.VIN) ? null : dto.Vehicle.VIN.Trim().ToUpperInvariant();
        var normalizedLicensePlate = string.IsNullOrWhiteSpace(dto.Vehicle.LicensePlate) ? null : dto.Vehicle.LicensePlate.Trim().ToUpperInvariant();

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
                    var mileageMatch = mileageText == null ? null : Regex.Match(mileageText, "([0-9\\s]+)");
                    if (mileageMatch != null && mileageMatch.Success)
                    {
                        var digits = mileageMatch.Groups[1].Value.Replace(" ", "");
                        if (int.TryParse(digits, out var mileageMil))
                            result.MileageKm = mileageMil * 10;
                    }
                }

                if (root.TryGetProperty("breadcrumb", out var breadcrumb) &&
                    breadcrumb.TryGetProperty("itemListElement", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty("position", out var positionNode) ||
                            !int.TryParse(positionNode.GetString(), out var position) ||
                            position != 3)
                        {
                            continue;
                        }

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

        result.InTraffic ??= GetSpec(specs, "I trafik");
        result.SwedishSold ??= GetSpec(specs, "Svensksåld");
        result.ColorName ??= GetSpec(specs, "Färg");
        result.OwnerCount ??= GetSpec(specs, "Antal ägare");
        result.Vin ??= GetSpec(specs, "Chassinummer (vin)");
        result.BodyType ??= GetSpec(specs, "Kaross");
        result.Classification ??= GetSpec(specs, "Klassificering");
        result.Generation ??= GetSpec(specs, "Generation");
        result.Engine ??= GetSpec(specs, "Motor");
        result.Gearbox ??= GetSpecAny(specs, "Växellåda", "Transmission", "Gearbox");
        result.DriveTrain ??= GetSpecAny(specs, "Drivlina", "Drivning", "Drivhjul", "Drive train");
        result.FuelConsumptionMixed ??= GetSpec(specs, "Blandad förbrukning") ?? GetSpec(specs, "Blandad");
        result.Co2Mixed ??= GetSpec(specs, "CO₂, Blandad");
        result.CargoVolume ??= GetSpec(specs, "Bagagevolym");
        result.SeatCount ??= GetSpec(specs, "Antal sittplatser");
        result.FuelType ??= GetSpec(specs, "Bränsle") ?? result.FuelType;

        var plainText = CleanHtmlText(html);

        result.ColorName ??= ExtractFact(plainText, "Färg", "Antal ägare");

        if (result.MileageKm == null)
        {
            var mileage = GetSpec(specs, "Mätarställning") ?? ExtractFact(plainText, "Mätarställning", "Rapporterade mätarställningar");
            if (!string.IsNullOrWhiteSpace(mileage))
            {
                var mileageKm = ParseMileageFromText(mileage);
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

    private static async Task<string?> TryFetchRenderedPageHtmlAsync(string url)
    {
        var browserPath = GetEdgeExecutablePath();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            
            BrowserTypeLaunchOptions launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled"
                }
            };
            
            // Set custom executable path only if one was found
            if (!string.IsNullOrEmpty(browserPath))
            {
                launchOptions.ExecutablePath = browserPath;
            }

            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "sv-SE",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1600, Height = 2400 }
            });

            var page = await context.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 45000
            });

            var expandButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Visa fler" });
            if (await expandButton.CountAsync() > 0 && await expandButton.First.IsVisibleAsync())
            {
                await expandButton.First.ClickAsync();
            }

            await page.WaitForTimeoutAsync(5000);
            return await page.ContentAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rendered page fetch failed: {ex.Message}");
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

        // Linux/Docker paths for Chromium
        var linuxCandidates = new[]
        {
            "/usr/bin/chromium",              // Debian/Ubuntu chromium package
            "/snap/bin/chromium",             // Snap chromium (works in Docker if snappy is available)
            "/usr/bin/chromium-browser"       // Old package name
        };

        foreach (var candidate in linuxCandidates)
        {
            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        // If in Docker and no browser found, still try /usr/bin/chromium as fallback
        // The package should be installed even if File.Exists check fails
        var runningInContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
        if (runningInContainer)
        {
            return "/usr/bin/chromium";
        }

        // In non-Docker environment, return null to let Playwright use its bundled Chromium
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
        var match = Regex.Match(text, "([0-9][0-9\\s]*)\\s*mil", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var digits = match.Groups[1].Value.Replace(" ", "");
        if (!int.TryParse(digits, out var mileageMil))
            return null;

        return mileageMil * 10;
    }
}
