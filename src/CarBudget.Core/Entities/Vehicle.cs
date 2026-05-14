namespace CarBudget.Core.Entities;

public class Vehicle
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
