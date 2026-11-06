using System.Net;
using System.Net.Http.Json;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.RoomTypes;
using StayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace StayDesk.Api.Tests.Integration;

public class RoomTypesTests : IClassFixture<StayDeskWebApplicationFactory>
{
    private readonly StayDeskWebApplicationFactory _factory;

    public RoomTypesTests(StayDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsPagedCatalog()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/room-types?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RoomTypeDto>>();
        Assert.NotNull(page);
        Assert.True(page!.Items.Count <= 2);
        Assert.True(page.TotalCount >= 3); // catalogo sembrado tiene 3 tipos
    }

    [Fact]
    public async Task SearchAvailability_ExcludesRoomTypeWithoutEnoughCapacity()
    {
        var client = _factory.CreateClient();
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 5, capacity: 2, basePrice: 1500m);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));
        var checkOut = checkIn.AddDays(2);

        var response = await client.GetAsync($"/api/room-types/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests=10&pageSize=50");

        var page = await response.Content.ReadFromJsonAsync<PagedResult<RoomTypeAvailabilityDto>>();
        Assert.DoesNotContain(page!.Items, r => r.RoomTypeId == roomTypeId);
    }

    [Fact]
    public async Task SearchAvailability_ReflectsRemainingRoomsAfterBooking()
    {
        var client = _factory.CreateClient();
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 1, capacity: 2, basePrice: 1500m);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var checkOut = checkIn.AddDays(2);

        var guestClient = await _factory.CreateAuthenticatedClientAsync("avail-guest");
        var created = await guestClient.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await client.GetAsync($"/api/room-types/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests=1&pageSize=50");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RoomTypeAvailabilityDto>>();

        Assert.DoesNotContain(page!.Items, r => r.RoomTypeId == roomTypeId); // ya no queda ninguna habitacion libre
    }

    [Fact]
    public async Task AddSeasonalRate_AsGuest_ReturnsForbidden()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var client = await _factory.CreateAuthenticatedClientAsync("seasonal-guest");

        var response = await client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/seasonal-rates", new
        {
            name = "Temporada alta",
            startDate = new DateOnly(2026, 12, 1),
            endDate = new DateOnly(2026, 12, 31),
            pricePerNight = 5000m
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddSeasonalRate_AsFrontDesk_IsCreatedAndAffectsPrice()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2, basePrice: 1000m);
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("seasonal-staff", UserRole.FrontDesk);

        var createResponse = await frontDesk.PostAsJsonAsync($"/api/room-types/{roomTypeId}/seasonal-rates", new
        {
            name = "Temporada alta",
            startDate = new DateOnly(2026, 12, 1),
            endDate = new DateOnly(2026, 12, 31),
            pricePerNight = 5000m
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var anonClient = _factory.CreateClient();
        var checkIn = new DateOnly(2026, 12, 10);
        var checkOut = checkIn.AddDays(1);
        var availability = await anonClient.GetAsync($"/api/room-types/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests=1&pageSize=50");
        var page = await availability.Content.ReadFromJsonAsync<PagedResult<RoomTypeAvailabilityDto>>();

        var room = page!.Items.Single(r => r.RoomTypeId == roomTypeId);
        Assert.Equal(5000m, room.TotalPrice);
    }
}
