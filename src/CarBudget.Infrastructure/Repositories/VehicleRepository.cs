using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using CarBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarBudget.Infrastructure.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly CarBudgetDbContext _context;

    public VehicleRepository(CarBudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Vehicle>> GetAllAsync()
    {
        return await _context.Vehicles
            .Include(v => v.Expenses)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByIdAsync(int id)
    {
        return await _context.Vehicles
            .Include(v => v.Expenses)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Vehicle?> GetByLicensePlateAsync(string licensePlate)
    {
        var normalizedLicensePlate = licensePlate.Trim().ToUpperInvariant();

        return await _context.Vehicles
            .Include(v => v.Expenses)
            .FirstOrDefaultAsync(v => v.LicensePlate == normalizedLicensePlate);
    }

    public async Task<Vehicle> AddAsync(Vehicle vehicle)
    {
        NormalizeVehicle(vehicle);
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        return vehicle;
    }

    public async Task UpdateAsync(Vehicle vehicle)
    {
        NormalizeVehicle(vehicle);
        vehicle.UpdatedAt = DateTime.UtcNow;
        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle != null)
        {
            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
        }
    }

    private static void NormalizeVehicle(Vehicle vehicle)
    {
        vehicle.Make = vehicle.Make.Trim();
        vehicle.Model = vehicle.Model.Trim();
        vehicle.Color = string.IsNullOrWhiteSpace(vehicle.Color) ? null : vehicle.Color.Trim();
        vehicle.PhotoDataUrl = string.IsNullOrWhiteSpace(vehicle.PhotoDataUrl) ? null : vehicle.PhotoDataUrl.Trim();
        vehicle.VIN = string.IsNullOrWhiteSpace(vehicle.VIN) ? null : vehicle.VIN.Trim().ToUpperInvariant();
        vehicle.LicensePlate = string.IsNullOrWhiteSpace(vehicle.LicensePlate) ? null : vehicle.LicensePlate.Trim().ToUpperInvariant();
    }
}
