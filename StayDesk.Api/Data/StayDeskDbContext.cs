using Microsoft.EntityFrameworkCore;
using StayDesk.Api.Domain.Entities;

namespace StayDesk.Api.Data;

public class StayDeskDbContext : DbContext
{
    public StayDeskDbContext(DbContextOptions<StayDeskDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<SeasonalRate> SeasonalRates => Set<SeasonalRate>();
    public DbSet<RoomTypeDailyInventory> RoomTypeDailyInventories => Set<RoomTypeDailyInventory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.Property(rt => rt.Name).HasMaxLength(100).IsRequired();
            entity.Property(rt => rt.Description).HasMaxLength(1000);
            entity.Property(rt => rt.BasePricePerNight).HasPrecision(10, 2);
            entity.Property(rt => rt.WeekendSurchargePercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<SeasonalRate>(entity =>
        {
            entity.Property(sr => sr.Name).HasMaxLength(100).IsRequired();
            entity.Property(sr => sr.PricePerNight).HasPrecision(10, 2);

            entity.HasOne(sr => sr.RoomType)
                .WithMany(rt => rt.SeasonalRates)
                .HasForeignKey(sr => sr.RoomTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomTypeDailyInventory>(entity =>
        {
            entity.HasIndex(x => new { x.RoomTypeId, x.Date }).IsUnique();

            entity.HasOne(x => x.RoomType)
                .WithMany()
                .HasForeignKey(x => x.RoomTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasIndex(r => r.ConfirmationCode).IsUnique();
            entity.Property(r => r.ConfirmationCode).HasMaxLength(10).IsRequired();
            entity.Property(r => r.TotalPrice).HasPrecision(10, 2);
            entity.Property(r => r.PenaltyAmount).HasPrecision(10, 2);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();

            entity.HasOne(r => r.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.RoomType)
                .WithMany(rt => rt.Reservations)
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
