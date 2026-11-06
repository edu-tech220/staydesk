using Microsoft.EntityFrameworkCore;
using StayDesk.Api.Common.Exceptions;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.RoomTypes;
using StayDesk.Api.Services.Availability;
using StayDesk.Api.Services.Pricing;

namespace StayDesk.Api.Services.RoomTypes;

public class RoomTypeService : IRoomTypeService
{
    private readonly StayDeskDbContext _db;
    private readonly IPricingService _pricingService;
    private readonly IAvailabilityService _availabilityService;

    public RoomTypeService(StayDeskDbContext db, IPricingService pricingService, IAvailabilityService availabilityService)
    {
        _db = db;
        _pricingService = pricingService;
        _availabilityService = availabilityService;
    }

    public async Task<PagedResult<RoomTypeDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Se ordena por Id en SQL (SQLite, usado en pruebas, no sabe ordenar por decimal) y por
        // precio en memoria: el catalogo es pequeno, asi que el costo es insignificante.
        var all = await _db.RoomTypes.AsNoTracking()
            .OrderBy(rt => rt.Id)
            .Select(rt => new RoomTypeDto(rt.Id, rt.Name, rt.Description, rt.Capacity, rt.TotalRooms, rt.BasePricePerNight, rt.WeekendSurchargePercent))
            .ToListAsync(ct);

        var ordered = all.OrderBy(rt => rt.BasePricePerNight).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<RoomTypeDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = ordered.Count };
    }

    public async Task<PagedResult<RoomTypeAvailabilityDto>> SearchAvailabilityAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        int guests,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (checkOut <= checkIn)
        {
            throw new ValidationFailedException("La fecha de salida debe ser posterior a la fecha de entrada.");
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // El catalogo de tipos de habitacion es pequeno (tipicamente decenas, no millones),
        // asi que se trae completo y se calcula disponibilidad/precio en memoria; la paginacion
        // se aplica despues para mantener un contrato de API consistente con el resto del servicio.
        var roomTypes = await _db.RoomTypes.AsNoTracking()
            .Where(rt => rt.Capacity >= guests)
            .OrderBy(rt => rt.Id)
            .ToListAsync(ct);

        var results = new List<RoomTypeAvailabilityDto>();

        foreach (var roomType in roomTypes)
        {
            var maxBooked = await _availabilityService.GetMaxBookedAsync(roomType.Id, checkIn, checkOut, ct);
            var available = roomType.TotalRooms - maxBooked;
            if (available <= 0)
            {
                continue;
            }

            var seasonalRates = await _db.SeasonalRates.AsNoTracking()
                .Where(sr => sr.RoomTypeId == roomType.Id && sr.StartDate < checkOut && sr.EndDate >= checkIn)
                .ToListAsync(ct);

            var totalPrice = _pricingService.CalculateTotalPrice(roomType, seasonalRates, checkIn, checkOut);
            var nights = checkOut.DayNumber - checkIn.DayNumber;

            results.Add(new RoomTypeAvailabilityDto(
                roomType.Id,
                roomType.Name,
                roomType.Description,
                roomType.Capacity,
                roomType.TotalRooms,
                available,
                nights,
                totalPrice));
        }

        var orderedResults = results.OrderBy(r => r.TotalPrice).ToList();
        var pageItems = orderedResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<RoomTypeAvailabilityDto>
        {
            Items = pageItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = results.Count
        };
    }

    public async Task<IReadOnlyList<SeasonalRateDto>> GetSeasonalRatesAsync(int roomTypeId, CancellationToken ct = default)
    {
        await EnsureRoomTypeExistsAsync(roomTypeId, ct);

        return await _db.SeasonalRates.AsNoTracking()
            .Where(sr => sr.RoomTypeId == roomTypeId)
            .OrderBy(sr => sr.StartDate)
            .Select(sr => new SeasonalRateDto(sr.Id, sr.RoomTypeId, sr.Name, sr.StartDate, sr.EndDate, sr.PricePerNight))
            .ToListAsync(ct);
    }

    public async Task<SeasonalRateDto> AddSeasonalRateAsync(int roomTypeId, CreateSeasonalRateRequest request, CancellationToken ct = default)
    {
        await EnsureRoomTypeExistsAsync(roomTypeId, ct);

        if (request.EndDate < request.StartDate)
        {
            throw new ValidationFailedException("La fecha final de la temporada debe ser posterior o igual a la inicial.");
        }

        var rate = new SeasonalRate
        {
            RoomTypeId = roomTypeId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            PricePerNight = request.PricePerNight
        };

        _db.SeasonalRates.Add(rate);
        await _db.SaveChangesAsync(ct);

        return new SeasonalRateDto(rate.Id, rate.RoomTypeId, rate.Name, rate.StartDate, rate.EndDate, rate.PricePerNight);
    }

    private async Task EnsureRoomTypeExistsAsync(int roomTypeId, CancellationToken ct)
    {
        var exists = await _db.RoomTypes.AsNoTracking().AnyAsync(rt => rt.Id == roomTypeId, ct);
        if (!exists)
        {
            throw new NotFoundException("Tipo de habitacion no encontrado.");
        }
    }
}
