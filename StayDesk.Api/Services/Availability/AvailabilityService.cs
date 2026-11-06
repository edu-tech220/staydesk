using Microsoft.EntityFrameworkCore;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;

namespace StayDesk.Api.Services.Availability;

public class AvailabilityService : IAvailabilityService
{
    private readonly StayDeskDbContext _db;

    public AvailabilityService(StayDeskDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryHoldAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, int totalRooms, CancellationToken ct = default)
    {
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            var held = await TryIncrementDateAsync(roomTypeId, date, totalRooms, ct);
            if (!held)
            {
                return false;
            }
        }

        return true;
    }

    public async Task ReleaseAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
    {
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE RoomTypeDailyInventories SET RoomsBooked = RoomsBooked - 1 WHERE RoomTypeId = {roomTypeId} AND Date = {date} AND RoomsBooked > 0",
                ct);
        }
    }

    public async Task<int> GetMaxBookedAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
    {
        var booked = await _db.RoomTypeDailyInventories
            .AsNoTracking()
            .Where(x => x.RoomTypeId == roomTypeId && x.Date >= checkIn && x.Date < checkOut)
            .Select(x => x.RoomsBooked)
            .ToListAsync(ct);

        return booked.Count == 0 ? 0 : booked.Max();
    }

    /// <summary>
    /// Incrementa de forma atomica el contador del dia si hay cupo. El UPDATE con condicion
    /// en el WHERE es seguro bajo concurrencia sin necesitar hints de bloqueo (UPDLOCK/HOLDLOCK):
    /// el motor toma el lock de la fila antes de evaluar el predicado, asi que dos conexiones
    /// concurrentes nunca incrementan mas alla del limite. Se evitan los hints T-SQL porque no
    /// son portables a SQLite, el motor usado en las pruebas automatizadas.
    /// </summary>
    private async Task<bool> TryIncrementDateAsync(int roomTypeId, DateOnly date, int totalRooms, CancellationToken ct)
    {
        var updated = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE RoomTypeDailyInventories SET RoomsBooked = RoomsBooked + 1 WHERE RoomTypeId = {roomTypeId} AND Date = {date} AND RoomsBooked < {totalRooms}",
            ct);

        if (updated > 0)
        {
            return true;
        }

        var rowExists = await _db.RoomTypeDailyInventories
            .AsNoTracking()
            .AnyAsync(x => x.RoomTypeId == roomTypeId && x.Date == date, ct);

        if (rowExists)
        {
            return false; // ya esta al limite de habitaciones disponibles ese dia
        }

        try
        {
            _db.RoomTypeDailyInventories.Add(new RoomTypeDailyInventory
            {
                RoomTypeId = roomTypeId,
                Date = date,
                RoomsBooked = 1
            });
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Otra transaccion concurrente gano la carrera de creacion de la fila del dia.
            // Se descarta el intento de insert y se reintenta como UPDATE puro.
            foreach (var entry in _db.ChangeTracker.Entries<RoomTypeDailyInventory>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var retried = await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE RoomTypeDailyInventories SET RoomsBooked = RoomsBooked + 1 WHERE RoomTypeId = {roomTypeId} AND Date = {date} AND RoomsBooked < {totalRooms}",
                ct);
            return retried > 0;
        }
    }
}
