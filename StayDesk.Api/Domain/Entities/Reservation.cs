using StayDesk.Api.Domain.Enums;

namespace StayDesk.Api.Domain.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int GuestsCount { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public decimal TotalPrice { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Token de concurrencia regenerado en cada transicion de estado. Se usa en vez de
    /// rowversion porque rowversion no es portable a SQLite (motor usado en las pruebas).
    /// </summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
