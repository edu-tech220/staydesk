using System.Net;
using System.Net.Http.Json;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Auth;
using StayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace StayDesk.Api.Tests.Integration;

public class AuthTests : IClassFixture<StayDeskWebApplicationFactory>
{
    private readonly StayDeskWebApplicationFactory _factory;

    public AuthTests(StayDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsTokenAndGuestRole()
    {
        var client = _factory.CreateClient();
        var email = $"nuevo-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Ana Lopez", email, password = "Password#123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal("Guest", auth.Role);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = $"duplicado-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Primero", email, password = "Password#123" });

        var response = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Segundo", email, password = "Password#456" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Carlos Diaz", email, password = "Password#123" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "incorrecta" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reservations/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FrontDeskOnlyEndpoint_WithGuestToken_ReturnsForbidden()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("guest-forbidden");

        var response = await client.GetAsync("/api/reservations?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FrontDeskOnlyEndpoint_WithFrontDeskToken_Succeeds()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("frontdesk", UserRole.FrontDesk);

        var response = await client.GetAsync("/api/reservations?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
