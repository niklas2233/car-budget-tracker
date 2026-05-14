namespace CarBudget.Core.Entities;

public class Expense
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public ExpenseType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? Mileage { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public decimal? Shipping { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}

public enum ExpenseType
{
    Fuel = 1,
    Maintenance = 2,
    Repair = 3,
    Insurance = 4,
    Registration = 5,
    Parking = 6,
    Tolls = 7,
    Wash = 8,
    SpareParts = 10,
    Other = 9
}
