using CarBudget.Core.Entities;

namespace CarBudget.Core.Interfaces;

public interface IExpenseRepository
{
    Task<IEnumerable<Expense>> GetAllAsync();
    Task<IEnumerable<Expense>> GetByVehicleIdAsync(int vehicleId);
    Task<Expense?> GetByIdAsync(int id);
    Task<Expense> AddAsync(Expense expense);
    Task UpdateAsync(Expense expense);
    Task DeleteAsync(int id);
    Task<decimal> GetTotalExpensesByVehicleIdAsync(int vehicleId);
    Task<IEnumerable<Expense>> GetExpensesByDateRangeAsync(int vehicleId, DateTime startDate, DateTime endDate);
}
