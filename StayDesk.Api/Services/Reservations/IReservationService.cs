using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.Reservations;

namespace StayDesk.Api.Services.Reservations;

public interface IReservationService
{
    Task<ReservationDto> CreateAsync(int userId, CreateReservationRequest request, CancellationToken ct = default);

    Task<ReservationDto> GetByIdAsync(int id, int requestingUserId, UserRole requestingUserRole, CancellationToken ct = default);

    Task<PagedResult<ReservationDto>> GetMineAsync(int userId, int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<ReservationDto>> GetAllAsync(
        ReservationStatus? status,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ReservationDto> ConfirmAsync(int id, CancellationToken ct = default);

    Task<ReservationDto> CheckInAsync(int id, CancellationToken ct = default);

    Task<ReservationDto> CheckOutAsync(int id, CancellationToken ct = default);

    Task<ReservationDto> CancelAsync(int id, int requestingUserId, UserRole requestingUserRole, CancellationToken ct = default);

    Task<ReservationDto> MarkNoShowAsync(int id, CancellationToken ct = default);
}
