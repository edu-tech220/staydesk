using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Reservations;
using StayDesk.Api.Services.Availability;
using StayDesk.Api.Services.Pricing;
using StayDesk.Api.Services.Reservations;
using StayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace StayDesk.Api.Tests.Integration;

/// <summary>
/// Ejercita ReservationService directamente (sin pasar por HTTP) con un reloj controlable,
/// para poder probar el camino de NoShow: requiere que la fecha de entrada ya haya pasado,
/// algo que una prueba via API no puede simular sin viajar en el tiempo.
/// </summary>
public class ReservationServiceDirectTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly StayDeskDbContext _db;
    private readonly TestClock _clock = new() { UtcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc) };
    private readonly ReservationService _sut;
    private readonly int _userId;
    private readonly int _roomTypeId;

    public ReservationServiceDirectTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<StayDeskDbContext>().UseSqlite(_connection).Options;
        _db = new StayDeskDbContext(options);
        _db.Database.EnsureCreated();

        var user = new User { FullName = "Huesped Directo", Email = "directo@test.local", PasswordHash = "n/a", Role = UserRole.Guest, CreatedAtUtc = _clock.UtcNow };
        var roomType = new RoomType { Name = "Habitacion Directa", Capacity = 2, TotalRooms = 1, BasePricePerNight = 1000m, WeekendSurchargePercent = 0m };
        _db.Users.Add(user);
        _db.RoomTypes.Add(roomType);
        _db.SaveChanges();
        _userId = user.Id;
        _roomTypeId = roomType.Id;

        var availability = new AvailabilityService(_db);
        _sut = new ReservationService(_db, new PricingService(), availability, _clock);
    }

    [Fact]
    public async Task MarkNoShow_AfterCheckInDatePasses_SetsFullPenalty()
    {
        var checkIn = _clock.Today.AddDays(5);
        var created = await _sut.CreateAsync(_userId, new CreateReservationRequest(_roomTypeId, checkIn, checkIn.AddDays(2), 1));
        await _sut.ConfirmAsync(created.Id);

        _clock.UtcNow = _clock.UtcNow.AddDays(6); // avanza el reloj mas alla de la fecha de entrada

        var result = await _sut.MarkNoShowAsync(created.Id);

        Assert.Equal("NoShow", result.Status);
        Assert.Equal(created.TotalPrice, result.PenaltyAmount);
    }

    [Fact]
    public async Task MarkNoShow_WhenNotConfirmed_ThrowsConflict()
    {
        var checkIn = _clock.Today.AddDays(5);
        var created = await _sut.CreateAsync(_userId, new CreateReservationRequest(_roomTypeId, checkIn, checkIn.AddDays(2), 1));

        _clock.UtcNow = _clock.UtcNow.AddDays(6);

        await Assert.ThrowsAsync<StayDesk.Api.Common.Exceptions.ConflictException>(() => _sut.MarkNoShowAsync(created.Id));
    }

    [Fact]
    public async Task Cancel_ExactlyThreeDaysBefore_IsFree()
    {
        var checkIn = _clock.Today.AddDays(3);
        var created = await _sut.CreateAsync(_userId, new CreateReservationRequest(_roomTypeId, checkIn, checkIn.AddDays(1), 1));

        var cancelled = await _sut.CancelAsync(created.Id, _userId, UserRole.Guest);

        Assert.Equal(0m, cancelled.PenaltyAmount);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
