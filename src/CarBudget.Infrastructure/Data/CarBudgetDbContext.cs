using CarBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarBudget.Infrastructure.Data;

public class CarBudgetDbContext : DbContext
{
    public CarBudgetDbContext(DbContextOptions<CarBudgetDbContext> options) : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<VehicleLookupCache> VehicleLookupCaches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Make).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Year).IsRequired();
            entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SellPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.VIN).HasMaxLength(17);
            entity.Property(e => e.LicensePlate).HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(50);

            entity.HasIndex(e => new { e.Make, e.Model, e.Year });
            entity.HasIndex(e => e.PurchaseDate);
            entity.HasIndex(e => e.SellDate);
            entity.HasIndex(e => e.VIN).IsUnique();
            entity.HasIndex(e => e.LicensePlate).IsUnique();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Vehicles_Year_Range", "Year >= 1886 AND Year <= 2100");
                t.HasCheckConstraint("CK_Vehicles_PurchasePrice_NonNegative", "PurchasePrice >= 0");
                t.HasCheckConstraint("CK_Vehicles_Mileage_NonNegative", "Mileage IS NULL OR Mileage >= 0");
                t.HasCheckConstraint("CK_Vehicles_SellPrice_NonNegative", "SellPrice IS NULL OR SellPrice >= 0");
            });
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Vendor).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasIndex(e => new { e.VehicleId, e.Date });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Expenses_Amount_NonNegative", "Amount >= 0");
                t.HasCheckConstraint("CK_Expenses_Mileage_NonNegative", "Mileage IS NULL OR Mileage >= 0");
            });

            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.Expenses)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleLookupCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LicensePlate).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Vin).HasMaxLength(50);
            entity.Property(e => e.InTraffic).HasMaxLength(20);
            entity.Property(e => e.SwedishSold).HasMaxLength(20);
            entity.Property(e => e.ColorName).HasMaxLength(50);
            entity.Property(e => e.OwnerCount).HasMaxLength(20);
            entity.Property(e => e.BodyType).HasMaxLength(100);
            entity.Property(e => e.Classification).HasMaxLength(150);
            entity.Property(e => e.Generation).HasMaxLength(100);
            entity.Property(e => e.Engine).HasMaxLength(200);
            entity.Property(e => e.FuelType).HasMaxLength(100);
            entity.Property(e => e.Gearbox).HasMaxLength(100);
            entity.Property(e => e.DriveTrain).HasMaxLength(100);
            entity.Property(e => e.FuelConsumptionMixed).HasMaxLength(50);
            entity.Property(e => e.Co2Mixed).HasMaxLength(50);
            entity.Property(e => e.CargoVolume).HasMaxLength(50);
            entity.Property(e => e.SeatCount).HasMaxLength(20);
            entity.Property(e => e.SpecificationsJson);
            entity.Property(e => e.RawHtml);
            entity.Property(e => e.SourceUrl).IsRequired().HasMaxLength(500);

            entity.HasIndex(e => e.LicensePlate).IsUnique();

            entity.ToTable("VehicleLookupCache", t =>
            {
                t.HasCheckConstraint("CK_VehicleLookupCache_Year_Range", "Year IS NULL OR (Year >= 1886 AND Year <= 2100)");
                t.HasCheckConstraint("CK_VehicleLookupCache_Mileage_NonNegative", "MileageKm IS NULL OR MileageKm >= 0");
            });
        });
    }
}
