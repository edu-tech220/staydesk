using System.ComponentModel.DataAnnotations;

namespace StayDesk.Api.Dtos.Reservations;

public record CreateReservationRequest(
    [Required] int RoomTypeId,
    [Required] DateOnly CheckIn,
    [Required] DateOnly CheckOut,
    [Range(1, 50)] int GuestsCount);

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
