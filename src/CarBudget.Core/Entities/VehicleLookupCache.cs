namespace CarBudget.Core.Entities;

public class VehicleLookupCache
{
    public int Id { get; set; }
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
    public string? SpecificationsJson { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}