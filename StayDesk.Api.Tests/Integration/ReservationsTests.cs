using System.Net;
using System.Net.Http.Json;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.Reservations;
using StayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace StayDesk.Api.Tests.Integration;

public class ReservationsTests : IClassFixture<StayDeskWebApplicationFactory>
{
    private readonly StayDeskWebApplicationFactory _factory;

    public ReservationsTests(StayDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsPendingReservation()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 3, basePrice: 1000m);
        var client = await _factory.CreateAuthenticatedClientAsync("create-ok");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var checkOut = checkIn.AddDays(3);

        var response = await client.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 2 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal("Pending", reservation!.Status);
        Assert.Equal(3000m, reservation.TotalPrice);
        Assert.False(string.IsNullOrWhiteSpace(reservation.ConfirmationCode));
    }

    [Fact]
    public async Task Create_WithCheckInInThePast_ReturnsBadRequest()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 3);
        var client = await _factory.CreateAuthenticatedClientAsync("create-past");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var checkOut = checkIn.AddDays(1);

        var response = await client.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithCheckOutBeforeCheckIn_ReturnsBadRequest()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 3);
        var client = await _factory.CreateAuthenticatedClientAsync("create-badrange");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var response = await client.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(-1), guestsCount = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTooManyGuests_ReturnsBadRequest()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 3, capacity: 2);
        var client = await _factory.CreateAuthenticatedClientAsync("create-overcap");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var response = await client.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenRoomTypeDoesNotExist_ReturnsNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("create-missing");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var response = await client.PostAsJsonAsync("/api/reservations", new { roomTypeId = 999_999, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenNoAvailability_ReturnsConflict()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var checkOut = checkIn.AddDays(2);

        var firstGuest = await _factory.CreateAuthenticatedClientAsync("conflict-first");
        var first = await firstGuest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var secondGuest = await _factory.CreateAuthenticatedClientAsync("conflict-second");
        var second = await secondGuest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task StateMachine_HappyPath_PendingToConfirmedToCheckedInToCheckedOut()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var guest = await _factory.CreateAuthenticatedClientAsync("state-happy");
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("state-happy-staff", UserRole.FrontDesk);

        // Se reserva a partir de hoy para poder hacer check-in en la misma prueba.
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow);
        var checkOut = checkIn.AddDays(2);
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var confirmResponse = await frontDesk.PutAsync($"/api/reservations/{reservation!.Id}/confirm", content: null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("Confirmed", (await confirmResponse.Content.ReadFromJsonAsync<ReservationDto>())!.Status);

        var checkInResponse = await frontDesk.PutAsync($"/api/reservations/{reservation.Id}/check-in", content: null);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);
        Assert.Equal("CheckedIn", (await checkInResponse.Content.ReadFromJsonAsync<ReservationDto>())!.Status);

        var checkOutResponse = await frontDesk.PutAsync($"/api/reservations/{reservation.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);
        Assert.Equal("CheckedOut", (await checkOutResponse.Content.ReadFromJsonAsync<ReservationDto>())!.Status);
    }

    [Fact]
    public async Task CheckIn_WhenReservationIsStillPending_ReturnsConflict()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var guest = await _factory.CreateAuthenticatedClientAsync("state-pending-checkin");
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("state-pending-checkin-staff", UserRole.FrontDesk);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var response = await frontDesk.PutAsync($"/api/reservations/{reservation!.Id}/check-in", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CheckIn_BeforeCheckInDate_ReturnsConflict()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var guest = await _factory.CreateAuthenticatedClientAsync("state-early-checkin");
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("state-early-checkin-staff", UserRole.FrontDesk);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();
        await frontDesk.PutAsync($"/api/reservations/{reservation!.Id}/confirm", content: null);

        var response = await frontDesk.PutAsync($"/api/reservations/{reservation.Id}/check-in", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(10, 0)] // cancela con 10 dias de anticipacion -> sin penalizacion
    [InlineData(2, 0.5)] // cancela con 2 dias de anticipacion -> 50%
    [InlineData(0, 1.0)] // cancela el mismo dia de entrada -> 100%
    public async Task Cancel_AppliesCancellationPolicyBasedOnDaysUntilCheckIn(int daysUntilCheckIn, double expectedPenaltyRatio)
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2, basePrice: 1000m);
        var guest = await _factory.CreateAuthenticatedClientAsync("cancel-policy");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysUntilCheckIn));
        var checkOut = checkIn.AddDays(1);
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var response = await guest.PutAsync($"/api/reservations/{reservation!.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal((decimal)(1000m * (decimal)expectedPenaltyRatio), cancelled.PenaltyAmount);
    }

    [Fact]
    public async Task Cancel_ReleasesInventoryForOtherGuests()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70));
        var checkOut = checkIn.AddDays(2);

        var firstGuest = await _factory.CreateAuthenticatedClientAsync("release-first");
        var created = await firstGuest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var cancelResponse = await firstGuest.PutAsync($"/api/reservations/{reservation!.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var secondGuest = await _factory.CreateAuthenticatedClientAsync("release-second");
        var second = await secondGuest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount = 1 });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Cancel_AfterCheckedIn_ReturnsConflict()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var guest = await _factory.CreateAuthenticatedClientAsync("cancel-after-checkin");
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("cancel-after-checkin-staff", UserRole.FrontDesk);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();
        await frontDesk.PutAsync($"/api/reservations/{reservation!.Id}/confirm", content: null);
        await frontDesk.PutAsync($"/api/reservations/{reservation.Id}/check-in", content: null);

        var response = await guest.PutAsync($"/api/reservations/{reservation.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAnotherGuest_ReturnsForbidden()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var owner = await _factory.CreateAuthenticatedClientAsync("owner-guest");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var created = await owner.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var stranger = await _factory.CreateAuthenticatedClientAsync("stranger-guest");
        var response = await stranger.GetAsync($"/api/reservations/{reservation!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnReservationsPaginated()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 10);
        var guest = await _factory.CreateAuthenticatedClientAsync("mine-guest");
        var baseCheckIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));

        for (var i = 0; i < 3; i++)
        {
            var checkIn = baseCheckIn.AddDays(i * 3);
            await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        }

        var response = await guest.GetAsync("/api/reservations/mine?page=1&pageSize=2");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ReservationDto>>();

        Assert.Equal(2, page!.Items.Count);
        Assert.True(page.TotalCount >= 3);
        Assert.All(page.Items, r => Assert.True(r.GuestName.Length > 0));
    }

    [Fact]
    public async Task MarkNoShow_BeforeCheckInDatePasses_ReturnsConflict()
    {
        var roomTypeId = await _factory.SeedRoomTypeAsync(totalRooms: 2);
        var guest = await _factory.CreateAuthenticatedClientAsync("noshow-early");
        var frontDesk = await _factory.CreateAuthenticatedClientAsync("noshow-early-staff", UserRole.FrontDesk);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var created = await guest.PostAsJsonAsync("/api/reservations", new { roomTypeId, checkIn, checkOut = checkIn.AddDays(1), guestsCount = 1 });
        var reservation = await created.Content.ReadFromJsonAsync<ReservationDto>();
        await frontDesk.PutAsync($"/api/reservations/{reservation!.Id}/confirm", content: null);

        var response = await frontDesk.PutAsync($"/api/reservations/{reservation.Id}/no-show", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
