using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

namespace StayDesk.Web.ApiClient;

/// <summary>
/// Unico punto de contacto del frontend con el microservicio StayDesk.Api. Adjunta el JWT
/// guardado como claim en la cookie de sesion y traduce las respuestas ProblemDetails de
/// error en un ApiResult legible por las Razor Pages.
/// </summary>
public class StayDeskApiClient
{
    public const string AccessTokenClaimType = "access_token";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StayDeskApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClientFactory.CreateClient("StayDeskApi");
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<ApiResult<AuthResponse>> RegisterAsync(string fullName, string email, string password, CancellationToken ct)
        => PostAnonymousAsync<AuthResponse>("api/auth/register", new { fullName, email, password }, ct);

    public Task<ApiResult<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct)
        => PostAnonymousAsync<AuthResponse>("api/auth/login", new { email, password }, ct);

    public async Task<ApiResult<PagedResult<RoomTypeAvailabilityDto>>> SearchAvailabilityAsync(
        DateOnly checkIn, DateOnly checkOut, int guests, CancellationToken ct)
    {
        var url = $"api/room-types/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests={guests}&pageSize=50";
        AttachToken();
        var response = await _httpClient.GetAsync(url, ct);
        return await ReadResultAsync<PagedResult<RoomTypeAvailabilityDto>>(response, ct);
    }

    public async Task<ApiResult<ReservationDto>> CreateReservationAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, int guestsCount, CancellationToken ct)
    {
        AttachToken();
        var response = await _httpClient.PostAsJsonAsync("api/reservations", new { roomTypeId, checkIn, checkOut, guestsCount }, ct);
        return await ReadResultAsync<ReservationDto>(response, ct);
    }

    public async Task<ApiResult<PagedResult<ReservationDto>>> GetMyReservationsAsync(int page, int pageSize, CancellationToken ct)
    {
        AttachToken();
        var response = await _httpClient.GetAsync($"api/reservations/mine?page={page}&pageSize={pageSize}", ct);
        return await ReadResultAsync<PagedResult<ReservationDto>>(response, ct);
    }

    public async Task<ApiResult<PagedResult<ReservationDto>>> GetAllReservationsAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        AttachToken();
        var statusQuery = string.IsNullOrWhiteSpace(status) ? string.Empty : $"&status={status}";
        var response = await _httpClient.GetAsync($"api/reservations?page={page}&pageSize={pageSize}{statusQuery}", ct);
        return await ReadResultAsync<PagedResult<ReservationDto>>(response, ct);
    }

    public Task<ApiResult<ReservationDto>> CancelReservationAsync(int id, CancellationToken ct) => PutAsync<ReservationDto>($"api/reservations/{id}/cancel", ct);

    public Task<ApiResult<ReservationDto>> ConfirmReservationAsync(int id, CancellationToken ct) => PutAsync<ReservationDto>($"api/reservations/{id}/confirm", ct);

    public Task<ApiResult<ReservationDto>> CheckInReservationAsync(int id, CancellationToken ct) => PutAsync<ReservationDto>($"api/reservations/{id}/check-in", ct);

    public Task<ApiResult<ReservationDto>> CheckOutReservationAsync(int id, CancellationToken ct) => PutAsync<ReservationDto>($"api/reservations/{id}/check-out", ct);

    public Task<ApiResult<ReservationDto>> MarkNoShowAsync(int id, CancellationToken ct) => PutAsync<ReservationDto>($"api/reservations/{id}/no-show", ct);

    private async Task<ApiResult<T>> PutAsync<T>(string url, CancellationToken ct)
    {
        AttachToken();
        var response = await _httpClient.PutAsync(url, content: null, ct);
        return await ReadResultAsync<T>(response, ct);
    }

    private async Task<ApiResult<T>> PostAnonymousAsync<T>(string url, object body, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var response = await _httpClient.PostAsJsonAsync(url, body, ct);
        return await ReadResultAsync<T>(response, ct);
    }

    private void AttachToken()
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue(AccessTokenClaimType);
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return ApiResult<T>.Success(data!, (int)response.StatusCode);
        }

        string message;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            message = problem?.Detail ?? problem?.Title ?? $"Error {(int)response.StatusCode}";
        }
        catch
        {
            message = $"Error {(int)response.StatusCode} al comunicarse con el servicio.";
        }

        return ApiResult<T>.Failure(message, (int)response.StatusCode);
    }
}
