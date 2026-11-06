using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Auth;
using Xunit;

namespace StayDesk.Api.Tests.Infrastructure;

/// <summary>
/// Reemplaza SQL Server por SQLite en memoria con cache compartido: cada DbContext abre su
/// propia conexion (como en produccion), pero todas apuntan a la misma base en memoria, lo que
/// permite probar concurrencia real entre requests simultaneas. "Default Timeout" configura el
/// busy_timeout de SQLite para que los escritores concurrentes esperen en vez de fallar de inmediato.
/// </summary>
public class StayDeskWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public StayDeskWebApplicationFactory()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"staydesk-tests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30
        };
        _connectionString = builder.ToString();

        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<StayDeskDbContext>>();
            services.AddDbContext<StayDeskDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db);
    }

    Task IAsyncLifetime.InitializeAsync() => InitializeDatabaseAsync();

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>Inserta un tipo de habitacion con parametros controlados, util para pruebas de precio y concurrencia.</summary>
    public async Task<int> SeedRoomTypeAsync(int totalRooms, int capacity = 2, decimal basePrice = 1000m, decimal weekendSurchargePercent = 0m)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();

        var roomType = new RoomType
        {
            Name = $"Habitacion de prueba {Guid.NewGuid():N}",
            Description = "Generada por pruebas automatizadas",
            Capacity = capacity,
            TotalRooms = totalRooms,
            BasePricePerNight = basePrice,
            WeekendSurchargePercent = weekendSurchargePercent
        };

        db.RoomTypes.Add(roomType);
        await db.SaveChangesAsync();
        return roomType.Id;
    }

    public async Task AddSeasonalRateAsync(int roomTypeId, DateOnly start, DateOnly end, decimal pricePerNight, string name = "Temporada de prueba")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();

        db.SeasonalRates.Add(new SeasonalRate
        {
            RoomTypeId = roomTypeId,
            Name = name,
            StartDate = start,
            EndDate = end,
            PricePerNight = pricePerNight
        });
        await db.SaveChangesAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string emailPrefix, UserRole role = UserRole.Guest)
    {
        var client = CreateClient();

        if (role == UserRole.FrontDesk)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "frontdesk@staydesk.local", password = "FrontDesk#2026" });
            login.EnsureSuccessStatusCode();
            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
            return client;
        }

        var email = $"{emailPrefix}-{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Huesped de prueba", email, password = "Password#123" });
        register.EnsureSuccessStatusCode();
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered!.Token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
