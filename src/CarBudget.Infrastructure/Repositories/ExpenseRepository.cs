using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarBudget.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly CarBudgetDbContext _context;

    public ExpenseRepository(CarBudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Expense>> GetAllAsync()
    {
        return await _context.Expenses
            .Include(e => e.Vehicle)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Expense>> GetByVehicleIdAsync(int vehicleId)
    {
        return await _context.Expenses
            .Where(e => e.VehicleId == vehicleId)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }

    public async Task<Expense?> GetByIdAsync(int id)
    {
        return await _context.Expenses
            .Include(e => e.Vehicle)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Expense> AddAsync(Expense expense)
    {
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();
        return expense;
    }

    public async Task UpdateAsync(Expense expense)
    {
        expense.UpdatedAt = DateTime.UtcNow;
        _context.Expenses.Update(expense);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense != null)
        {
            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<decimal> GetTotalExpensesByVehicleIdAsync(int vehicleId)
    {
        return await _context.Expenses
            .Where(e => e.VehicleId == vehicleId)
            .SumAsync(e => e.Type == ExpenseType.SpareParts ? e.Amount : e.Amount + (e.Shipping ?? 0));
    }

    public async Task<IEnumerable<Expense>> GetExpensesByDateRangeAsync(int vehicleId, DateTime startDate, DateTime endDate)
    {
        return await _context.Expenses
            .Where(e => e.VehicleId == vehicleId && e.Date >= startDate && e.Date <= endDate)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }
}
