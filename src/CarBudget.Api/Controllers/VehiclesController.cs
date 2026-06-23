using CarBudget.Api.DTOs;
using CarBudget.Api.Services;
using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CarBudget.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    internal static string Region = "sweden";

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly CarBudgetDbContext _dbContext;
    private readonly PlateScraperService _plateScraper;

    public VehiclesController(IVehicleRepository vehicleRepository, IExpenseRepository expenseRepository, CarBudgetDbContext dbContext, PlateScraperService plateScraper)
    {
        _vehicleRepository = vehicleRepository;
        _expenseRepository = expenseRepository;
        _dbContext = dbContext;
        _plateScraper = plateScraper;
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

        var normalizedPlate = PlateScraperService.NormalizePlate(licensePlate);
        if (normalizedPlate.Length < 5)
            return BadRequest("License plate looks invalid.");

        var cachedLookup = await _dbContext.VehicleLookupCaches
            .FirstOrDefaultAsync(x => x.LicensePlate == normalizedPlate);

        var hasRichCachedSpecs = cachedLookup != null &&
            !string.IsNullOrWhiteSpace(cachedLookup.SpecificationsJson);

        var hasCachedLookupFields = cachedLookup != null &&
            cachedLookup.MileageKm != null &&
            !string.IsNullOrWhiteSpace(cachedLookup.ColorName);

        if (hasRichCachedSpecs && hasCachedLookupFields && !forceRefresh)
            return Ok(MapCacheToDto(cachedLookup!));

        var url = Region == "norway"
            ? $"https://www.car.info/en-no/license-plate/N/{normalizedPlate}#specs"
            : $"https://www.car.info/en-se/license-plate/S/{normalizedPlate}#specs";

        var (dto, rateLimited, failed) = await _plateScraper.LookupAsync(normalizedPlate, url, forceRefresh);

        if (rateLimited)
        {
            if (!forceRefresh && hasCachedLookupFields)
                return Ok(MapCacheToDto(cachedLookup!));
            return StatusCode(429, "Rate limited by car.info. Wait a minute and try again.");
        }

        if (failed || dto == null)
        {
            if (!forceRefresh && hasCachedLookupFields)
                return Ok(MapCacheToDto(cachedLookup!));
            return StatusCode(503, "Could not render Car.info with Playwright. Try again in a minute.");
        }

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
}
