namespace StayDesk.Web.ApiClient;

// Contratos propios del frontend que reflejan la forma de las respuestas JSON del backend.
// Se duplican a proposito (en vez de referenciar el proyecto StayDesk.Api) para mantener
// ambos servicios genuinamente independientes y desplegables por separado.

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record AuthResponse(string Token, string FullName, string Email, string Role, DateTime ExpiresAtUtc);

public record RoomTypeAvailabilityDto(
    int RoomTypeId,
    string Name,
    string Description,
    int Capacity,
    int TotalRooms,
    int RoomsAvailable,
    int Nights,
    decimal TotalPrice);

public record ReservationDto(
    int Id,
    int UserId,
    string GuestName,
    int RoomTypeId,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int GuestsCount,
    string Status,
    decimal TotalPrice,
    decimal? PenaltyAmount,
    string ConfirmationCode,
    DateTime CreatedAtUtc);

public record ApiErrorResponse(string? Title, string? Detail, int? Status);
