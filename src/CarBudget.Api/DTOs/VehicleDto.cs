using CarBudget.Core.Entities;

namespace CarBudget.Api.DTOs;

public class VehicleDto
{
    public int Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? VIN { get; set; }
    public string? LicensePlate { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? Mileage { get; set; }
    public string? Color { get; set; }
    public string? Nickname { get; set; }
    public decimal? SellPrice { get; set; }
    public DateTime? SellDate { get; set; }
    public decimal TotalExpenses { get; set; }
    public int ExpenseCount { get; set; }
}

public class CreateVehicleDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? VIN { get; set; }
    public string? LicensePlate { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? Mileage { get; set; }
    public string? Color { get; set; }
    public string? Nickname { get; set; }
    public decimal? SellPrice { get; set; }
    public DateTime? SellDate { get; set; }
}

public class UpdateVehicleDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? VIN { get; set; }
    public string? LicensePlate { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? Mileage { get; set; }
    public string? Color { get; set; }
    public string? Nickname { get; set; }
    public decimal? SellPrice { get; set; }
    public DateTime? SellDate { get; set; }
}

public class VehicleLookupDto
{
    public string LicensePlate { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Vin { get; set; }
    public string? InTraffic { get; set; }
    public string? SwedishSold { get; set; }
    public string? ColorName { get; set; }
    public string? OwnerCount { get; set; }
    public int? MileageKm { get; set; }
    public string? BodyType { get; set; }
    public string? Classification { get; set; }
    public string? Generation { get; set; }
    public string? Engine { get; set; }
    public string? FuelType { get; set; }
    public string? Gearbox { get; set; }
    public string? DriveTrain { get; set; }
    public string? FuelConsumptionMixed { get; set; }
    public string? Co2Mixed { get; set; }
    public string? CargoVolume { get; set; }
    public string? SeatCount { get; set; }
    public Dictionary<string, string> Specifications { get; set; } = new();
    public string SourceUrl { get; set; } = string.Empty;
}

public class VehicleLookupCacheDto : VehicleLookupDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class VehicleExportPackageDto
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public VehicleExportDto Vehicle { get; set; } = new();
    public List<ExpenseExportDto> Expenses { get; set; } = new();
}

public class VehicleExportDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? VIN { get; set; }
    public string? LicensePlate { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? Mileage { get; set; }
    public string? Color { get; set; }
    public string? Nickname { get; set; }
    public decimal? SellPrice { get; set; }
    public DateTime? SellDate { get; set; }
}

public class ExpenseExportDto
{
    public ExpenseType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? Mileage { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public decimal? Shipping { get; set; }
}
