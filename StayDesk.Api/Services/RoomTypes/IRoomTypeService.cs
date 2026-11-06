using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.RoomTypes;

namespace StayDesk.Api.Services.RoomTypes;

public interface IRoomTypeService
{
    Task<PagedResult<RoomTypeDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<RoomTypeAvailabilityDto>> SearchAvailabilityAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        int guests,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<SeasonalRateDto>> GetSeasonalRatesAsync(int roomTypeId, CancellationToken ct = default);

    Task<SeasonalRateDto> AddSeasonalRateAsync(int roomTypeId, CreateSeasonalRateRequest request, CancellationToken ct = default);
}
