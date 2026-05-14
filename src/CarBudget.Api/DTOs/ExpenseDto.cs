using CarBudget.Core.Entities;

namespace CarBudget.Api.DTOs;

public class ExpenseDto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public ExpenseType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PhotoDataUrls { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? Mileage { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public decimal? Shipping { get; set; }
}

public class CreateExpenseDto
{
    public int VehicleId { get; set; }
    public ExpenseType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> PhotoDataUrls { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? Mileage { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public decimal? Shipping { get; set; }
}

public class UpdateExpenseDto
{
    public ExpenseType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> PhotoDataUrls { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? Mileage { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public decimal? Shipping { get; set; }
}
