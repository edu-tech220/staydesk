using Microsoft.AspNetCore.Identity;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Domain.Enums;

namespace StayDesk.Api.Data;

public static class DbSeeder
{
    /// <summary>
    /// Siembra datos de ejemplo. Asume que el esquema ya existe (vía migraciones en SQL
    /// Server o EnsureCreated en SQLite para pruebas) — esta clase no crea el esquema.
    /// </summary>
    public static async Task SeedAsync(StayDeskDbContext db)
    {
        if (!db.RoomTypes.Any())
        {
            db.RoomTypes.AddRange(
                new RoomType
                {
                    Name = "Habitacion Sencilla",
                    Description = "Una cama matrimonial, ideal para viajeros solos.",
                    Capacity = 2,
                    TotalRooms = 8,
                    BasePricePerNight = 900m,
                    WeekendSurchargePercent = 15m
                },
                new RoomType
                {
                    Name = "Habitacion Doble",
                    Description = "Dos camas individuales, pensada para familias pequenas.",
                    Capacity = 4,
                    TotalRooms = 6,
                    BasePricePerNight = 1400m,
                    WeekendSurchargePercent = 20m
                },
                new RoomType
                {
                    Name = "Suite Ejecutiva",
                    Description = "Sala independiente y escritorio, para viajes de negocio.",
                    Capacity = 2,
                    TotalRooms = 3,
                    BasePricePerNight = 2600m,
                    WeekendSurchargePercent = 10m
                }
            );

            await db.SaveChangesAsync();
        }

        if (!db.Users.Any(u => u.Role == UserRole.FrontDesk))
        {
            var hasher = new PasswordHasher<User>();
            var staff = new User
            {
                FullName = "Recepcion StayDesk",
                Email = "frontdesk@staydesk.local",
                Role = UserRole.FrontDesk,
                CreatedAtUtc = DateTime.UtcNow
            };
            staff.PasswordHash = hasher.HashPassword(staff, "FrontDesk#2026");
            db.Users.Add(staff);

            await db.SaveChangesAsync();
        }
    }
}
