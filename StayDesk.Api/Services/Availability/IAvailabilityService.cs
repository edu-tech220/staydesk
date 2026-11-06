namespace StayDesk.Api.Services.Availability;

/// <summary>
/// Opera siempre dentro de la transaccion ambiente del DbContext inyectado: quien la use
/// (tipicamente ReservationService) debe abrir la transaccion antes de llamar a TryHoldAsync
/// y confirmarla/revertirla despues, para que el hold de inventario y el cambio de estado de
/// la reserva sean una sola unidad atomica.
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Intenta reservar una unidad de inventario para cada noche del rango [checkIn, checkOut).
    /// Devuelve false en cuanto una noche esta al limite de habitaciones disponibles; los
    /// incrementos ya aplicados en esa misma llamada quedan pendientes de que el llamador
    /// revierta la transaccion.
    /// </summary>
    Task<bool> TryHoldAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, int totalRooms, CancellationToken ct = default);

    /// <summary>Libera una unidad de inventario para cada noche del rango, tipicamente al cancelar.</summary>
    Task ReleaseAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default);

    /// <summary>Numero maximo de habitaciones ocupadas en cualquier noche del rango (0 si no hay reservas).</summary>
    Task<int> GetMaxBookedAsync(int roomTypeId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default);
}
