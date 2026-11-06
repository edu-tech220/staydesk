using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Services.Pricing;
using Xunit;

namespace StayDesk.Api.Tests.Unit;

public class PricingServiceTests
{
    private readonly PricingService _sut = new();

    private static RoomType CreateRoomType(decimal basePrice = 1000m, decimal weekendSurchargePercent = 20m) => new()
    {
        Id = 1,
        Name = "Habitacion de prueba",
        Capacity = 2,
        TotalRooms = 5,
        BasePricePerNight = basePrice,
        WeekendSurchargePercent = weekendSurchargePercent
    };

    [Fact]
    public void CalculateTotalPrice_AllWeekdayNights_UsesBasePriceOnly()
    {
        var roomType = CreateRoomType();
        // 2026-01-05 es lunes -> lunes a miercoles, todas noches entre semana.
        var checkIn = new DateOnly(2026, 1, 5);
        var checkOut = new DateOnly(2026, 1, 7);

        var total = _sut.CalculateTotalPrice(roomType, Array.Empty<SeasonalRate>(), checkIn, checkOut);

        Assert.Equal(2000m, total);
    }

    [Fact]
    public void CalculateTotalPrice_IncludesWeekendNight_AppliesSurcharge()
    {
        var roomType = CreateRoomType(basePrice: 1000m, weekendSurchargePercent: 20m);
        // 2026-01-09 es viernes.
        var checkIn = new DateOnly(2026, 1, 9);
        var checkOut = new DateOnly(2026, 1, 10);

        var total = _sut.CalculateTotalPrice(roomType, Array.Empty<SeasonalRate>(), checkIn, checkOut);

        Assert.Equal(1200m, total);
    }

    [Fact]
    public void CalculateTotalPrice_SeasonalRateOverridesBaseAndWeekendSurcharge()
    {
        var roomType = CreateRoomType(basePrice: 1000m, weekendSurchargePercent: 50m);
        var checkIn = new DateOnly(2026, 12, 24);
        var checkOut = new DateOnly(2026, 12, 26);
        var seasonalRates = new[]
        {
            new SeasonalRate { RoomTypeId = 1, Name = "Navidad", StartDate = new DateOnly(2026, 12, 20), EndDate = new DateOnly(2026, 12, 31), PricePerNight = 3000m }
        };

        var total = _sut.CalculateTotalPrice(roomType, seasonalRates, checkIn, checkOut);

        Assert.Equal(6000m, total);
    }

    [Fact]
    public void CalculateTotalPrice_MixesSeasonalAndRegularNights()
    {
        var roomType = CreateRoomType(basePrice: 1000m, weekendSurchargePercent: 0m);
        // 2026-12-31 dentro de temporada, 2027-01-01 fuera de temporada (precio base).
        var checkIn = new DateOnly(2026, 12, 31);
        var checkOut = new DateOnly(2027, 1, 2);
        var seasonalRates = new[]
        {
            new SeasonalRate { RoomTypeId = 1, Name = "Fin de ano", StartDate = new DateOnly(2026, 12, 20), EndDate = new DateOnly(2026, 12, 31), PricePerNight = 3000m }
        };

        var total = _sut.CalculateTotalPrice(roomType, seasonalRates, checkIn, checkOut);

        Assert.Equal(4000m, total); // 3000 (31 dic, temporada) + 1000 (1 ene, base)
    }
}
