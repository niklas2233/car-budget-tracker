using CarBudget.Api.DTOs;
using CarBudget.Core.Entities;
using CarBudget.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CarBudget.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public ExpensesController(IExpenseRepository expenseRepository, IVehicleRepository vehicleRepository)
    {
        _expenseRepository = expenseRepository;
        _vehicleRepository = vehicleRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll()
    {
        var expenses = await _expenseRepository.GetAllAsync();
        return Ok(expenses.Select(MapToDto));
    }

    [HttpGet("vehicle/{vehicleId}")]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetByVehicleId(int vehicleId)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle == null)
            return NotFound("Vehicle not found");

        var expenses = await _expenseRepository.GetByVehicleIdAsync(vehicleId);
        return Ok(expenses.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExpenseDto>> GetById(int id)
    {
        var expense = await _expenseRepository.GetByIdAsync(id);
        if (expense == null)
            return NotFound();

        return Ok(MapToDto(expense));
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(CreateExpenseDto dto)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(dto.VehicleId);
        if (vehicle == null)
            return NotFound("Vehicle not found");

        var expense = new Expense
        {
            VehicleId = dto.VehicleId,
            Type = dto.Type,
            Description = dto.Description,
            PhotoDataUrlsJson = SerializePhotoDataUrls(dto.PhotoDataUrls),
            Amount = dto.Amount,
            Date = dto.Date,
            Mileage = dto.Mileage,
            Vendor = dto.Vendor,
            Notes = dto.Notes,
            Shipping = dto.Shipping
        };

        await _expenseRepository.AddAsync(expense);

        var createdExpense = await _expenseRepository.GetByIdAsync(expense.Id);
        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, MapToDto(createdExpense!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateExpenseDto dto)
    {
        var expense = await _expenseRepository.GetByIdAsync(id);
        if (expense == null)
            return NotFound();

        expense.Type = dto.Type;
        expense.Description = dto.Description;
        expense.PhotoDataUrlsJson = SerializePhotoDataUrls(dto.PhotoDataUrls);
        expense.Amount = dto.Amount;
        expense.Date = dto.Date;
        expense.Mileage = dto.Mileage;
        expense.Vendor = dto.Vendor;
        expense.Notes = dto.Notes;
        expense.Shipping = dto.Shipping;

        await _expenseRepository.UpdateAsync(expense);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _expenseRepository.GetByIdAsync(id);
        if (expense == null)
            return NotFound();

        await _expenseRepository.DeleteAsync(id);

        return NoContent();
    }

    private ExpenseDto MapToDto(Expense expense)
    {
        return new ExpenseDto
        {
            Id = expense.Id,
            VehicleId = expense.VehicleId,
            VehicleName = $"{expense.Vehicle.Year} {expense.Vehicle.Make} {expense.Vehicle.Model}",
            Type = expense.Type,
            TypeName = expense.Type.ToString(),
            Description = expense.Description,
            PhotoDataUrls = ParsePhotoDataUrls(expense.PhotoDataUrlsJson),
            Amount = expense.Amount,
            Date = expense.Date,
            Mileage = expense.Mileage,
            Vendor = expense.Vendor,
            Notes = expense.Notes,
            Shipping = expense.Shipping
        };
    }

    private static List<string> ParsePhotoDataUrls(string? photoDataUrlsJson)
    {
        if (string.IsNullOrWhiteSpace(photoDataUrlsJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(photoDataUrlsJson)
                ?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string? SerializePhotoDataUrls(IEnumerable<string>? photoDataUrls)
    {
        var normalized = photoDataUrls?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }
}
