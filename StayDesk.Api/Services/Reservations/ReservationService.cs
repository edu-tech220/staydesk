using Microsoft.EntityFrameworkCore;
using StayDesk.Api.Common;
using StayDesk.Api.Common.Exceptions;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.Reservations;
using StayDesk.Api.Services.Availability;
using StayDesk.Api.Services.Pricing;

namespace StayDesk.Api.Services.Reservations;

public class ReservationService : IReservationService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sin caracteres ambiguos

    private readonly StayDeskDbContext _db;
    private readonly IPricingService _pricingService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IClock _clock;

    public ReservationService(
        StayDeskDbContext db,
        IPricingService pricingService,
        IAvailabilityService availabilityService,
        IClock clock)
    {
        _db = db;
        _pricingService = pricingService;
        _availabilityService = availabilityService;
        _clock = clock;
    }

    public async Task<ReservationDto> CreateAsync(int userId, CreateReservationRequest request, CancellationToken ct = default)
    {
        var today = _clock.Today;

        if (request.CheckIn < today)
        {
            throw new ValidationFailedException("La fecha de entrada no puede ser en el pasado.");
        }

        if (request.CheckOut <= request.CheckIn)
        {
            throw new ValidationFailedException("La fecha de salida debe ser posterior a la fecha de entrada.");
        }

        var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;
        if (nights > 30)
        {
            throw new ValidationFailedException("Una estancia no puede superar 30 noches.");
        }

        var roomType = await _db.RoomTypes.AsNoTracking().FirstOrDefaultAsync(rt => rt.Id == request.RoomTypeId, ct)
            ?? throw new NotFoundException("El tipo de habitacion solicitado no existe.");

        if (request.GuestsCount > roomType.Capacity)
        {
            throw new ValidationFailedException($"Esta habitacion admite hasta {roomType.Capacity} huespedes.");
        }

        var seasonalRates = await _db.SeasonalRates.AsNoTracking()
            .Where(sr => sr.RoomTypeId == roomType.Id && sr.StartDate < request.CheckOut && sr.EndDate >= request.CheckIn)
            .ToListAsync(ct);

        var totalPrice = _pricingService.CalculateTotalPrice(roomType, seasonalRates, request.CheckIn, request.CheckOut);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var held = await _availabilityService.TryHoldAsync(roomType.Id, request.CheckIn, request.CheckOut, roomType.TotalRooms, ct);
        if (!held)
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("No hay disponibilidad para ese tipo de habitacion en las fechas solicitadas.");
        }

        var reservation = new Reservation
        {
            UserId = userId,
            RoomTypeId = roomType.Id,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            GuestsCount = request.GuestsCount,
            Status = ReservationStatus.Pending,
            TotalPrice = totalPrice,
            ConfirmationCode = GenerateConfirmationCode(),
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await LoadDtoAsync(reservation.Id, ct);
    }

    public async Task<ReservationDto> GetByIdAsync(int id, int requestingUserId, UserRole requestingUserRole, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);
        EnsureOwnerOrFrontDesk(reservation, requestingUserId, requestingUserRole);
        return ToDto(reservation);
    }

    public async Task<PagedResult<ReservationDto>> GetMineAsync(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Reservations.AsNoTracking().Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAtUtc);
        return await PaginateAsync(query, page, pageSize, ct);
    }

    public async Task<PagedResult<ReservationDto>> GetAllAsync(
        ReservationStatus? status,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Reservations.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        if (from is not null)
        {
            query = query.Where(r => r.CheckOut > from);
        }

        if (to is not null)
        {
            query = query.Where(r => r.CheckIn < to);
        }

        query = query.OrderByDescending(r => r.CreatedAtUtc);

        return await PaginateAsync(query, page, pageSize, ct);
    }

    public async Task<ReservationDto> ConfirmAsync(int id, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new ConflictException($"Solo se puede confirmar una reserva en estado Pending (actual: {reservation.Status}).");
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAtUtc = _clock.UtcNow;
        reservation.ConcurrencyStamp = Guid.NewGuid();

        await SaveWithConcurrencyCheckAsync(ct);
        return ToDto(reservation);
    }

    public async Task<ReservationDto> CheckInAsync(int id, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);

        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new ConflictException($"Solo se puede hacer check-in de una reserva Confirmed (actual: {reservation.Status}).");
        }

        if (_clock.Today < reservation.CheckIn)
        {
            throw new ConflictException("No se puede hacer check-in antes de la fecha de entrada.");
        }

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAtUtc = _clock.UtcNow;
        reservation.ConcurrencyStamp = Guid.NewGuid();

        await SaveWithConcurrencyCheckAsync(ct);
        return ToDto(reservation);
    }

    public async Task<ReservationDto> CheckOutAsync(int id, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);

        if (reservation.Status != ReservationStatus.CheckedIn)
        {
            throw new ConflictException($"Solo se puede hacer check-out de una reserva CheckedIn (actual: {reservation.Status}).");
        }

        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAtUtc = _clock.UtcNow;
        reservation.ConcurrencyStamp = Guid.NewGuid();

        await SaveWithConcurrencyCheckAsync(ct);
        return ToDto(reservation);
    }

    public async Task<ReservationDto> CancelAsync(int id, int requestingUserId, UserRole requestingUserRole, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);
        EnsureOwnerOrFrontDesk(reservation, requestingUserId, requestingUserRole);

        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
        {
            throw new ConflictException($"No se puede cancelar una reserva en estado {reservation.Status}.");
        }

        var penalty = CancellationPolicy.CalculatePenalty(reservation.TotalPrice, reservation.CheckIn, _clock.Today);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _availabilityService.ReleaseAsync(reservation.RoomTypeId, reservation.CheckIn, reservation.CheckOut, ct);

        reservation.Status = ReservationStatus.Cancelled;
        reservation.PenaltyAmount = penalty;
        reservation.CancelledAtUtc = _clock.UtcNow;
        reservation.ConcurrencyStamp = Guid.NewGuid();

        await SaveWithConcurrencyCheckAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDto(reservation);
    }

    public async Task<ReservationDto> MarkNoShowAsync(int id, CancellationToken ct = default)
    {
        var reservation = await FindTrackedAsync(id, ct);

        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new ConflictException($"Solo una reserva Confirmed puede marcarse como NoShow (actual: {reservation.Status}).");
        }

        if (_clock.Today <= reservation.CheckIn)
        {
            throw new ConflictException("Solo se puede marcar NoShow despues de que paso la fecha de entrada.");
        }

        reservation.Status = ReservationStatus.NoShow;
        reservation.PenaltyAmount = reservation.TotalPrice;
        reservation.ConcurrencyStamp = Guid.NewGuid();

        await SaveWithConcurrencyCheckAsync(ct);
        return ToDto(reservation);
    }

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La reserva fue modificada por otro proceso mientras tanto; vuelve a intentarlo.");
        }
    }

    private async Task<Reservation> FindTrackedAsync(int id, CancellationToken ct)
    {
        return await _db.Reservations
            .Include(r => r.User)
            .Include(r => r.RoomType)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Reserva no encontrada.");
    }

    private async Task<ReservationDto> LoadDtoAsync(int id, CancellationToken ct)
    {
        var reservation = await FindTrackedAsync(id, ct);
        return ToDto(reservation);
    }

    private static void EnsureOwnerOrFrontDesk(Reservation reservation, int requestingUserId, UserRole requestingUserRole)
    {
        if (requestingUserRole != UserRole.FrontDesk && reservation.UserId != requestingUserId)
        {
            throw new ForbiddenException("No tienes acceso a esta reserva.");
        }
    }

    private static async Task<PagedResult<ReservationDto>> PaginateAsync(IQueryable<Reservation> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReservationDto(
                r.Id,
                r.UserId,
                r.User!.FullName,
                r.RoomTypeId,
                r.RoomType!.Name,
                r.CheckIn,
                r.CheckOut,
                r.GuestsCount,
                r.Status.ToString(),
                r.TotalPrice,
                r.PenaltyAmount,
                r.ConfirmationCode,
                r.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<ReservationDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private static ReservationDto ToDto(Reservation reservation) => new(
        reservation.Id,
        reservation.UserId,
        reservation.User!.FullName,
        reservation.RoomTypeId,
        reservation.RoomType!.Name,
        reservation.CheckIn,
        reservation.CheckOut,
        reservation.GuestsCount,
        reservation.Status.ToString(),
        reservation.TotalPrice,
        reservation.PenaltyAmount,
        reservation.ConfirmationCode,
        reservation.CreatedAtUtc);

    private static string GenerateConfirmationCode()
    {
        Span<char> buffer = stackalloc char[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];
        }

        return new string(buffer);
    }
}
