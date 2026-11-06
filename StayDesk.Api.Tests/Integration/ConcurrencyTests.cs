using System.Net;
using System.Net.Http.Json;
using StayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace StayDesk.Api.Tests.Integration;

public class ConcurrencyTests : IClassFixture<StayDeskWebApplicationFactory>
{
    private readonly StayDeskWebApplicationFactory _factory;

    public ConcurrencyTests(StayDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateReservation_UnderConcurrentLoad_NeverExceedsRoomCapacity()
    {
        const int totalRooms = 2;
        const int concurrentGuests = 8;

        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: totalRooms, capacity: 4, basePrice: 1200m);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var checkOut = checkIn.AddDays(3);

        var clients = new List<HttpClient>();
        for (var i = 0; i < concurrentGuests; i++)
        {
            clients.Add(await _factory.CreateAuthenticatedClientAsync($"race-{i}"));
        }

        var tasks = clients.Select(client => client.PostAsJsonAsync(
            "/api/reservations",
            new { roomTypeId, checkIn, checkOut, guestsCount = 1 }));

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(totalRooms, successCount);
        Assert.Equal(concurrentGuests - totalRooms, conflictCount);

        // Verificacion cruzada contra la API de disponibilidad: no debe quedar ninguna habitacion libre.
        var availabilityClient = _factory.CreateClient();
        var availability = await availabilityClient.GetAsync(
            $"/api/room-types/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests=1&pageSize=50");
        var page = await availability.Content.ReadFromJsonAsync<StayDesk.Api.Dtos.Common.PagedResult<StayDesk.Api.Dtos.RoomTypes.RoomTypeAvailabilityDto>>();

        Assert.DoesNotContain(page!.Items, r => r.RoomTypeId == roomTypeId);
    }

    [Fact]
    public async Task CreateReservation_ConcurrentRequestsForDifferentDateRanges_AllSucceed()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 1, capacity: 4, basePrice: 1000m);
        var baseCheckIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(150));

        var clients = new List<HttpClient>();
        for (var i = 0; i < 5; i++)
        {
            clients.Add(await _factory.CreateAuthenticatedClientAsync($"nonoverlap-{i}"));
        }

        var tasks = clients.Select((client, index) =>
        {
            var checkIn = baseCheckIn.AddDays(index * 3); // rangos de fechas disjuntos
            return client.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(2), guestsCount = 1 });
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }
}
