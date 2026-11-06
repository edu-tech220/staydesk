using StayDesk.Api.Domain.Entities;

namespace StayDesk.Api.Services.Pricing;

public interface IPricingService
{
    /// <summary>
    /// Calcula el precio total de una estancia noche por noche: usa la tarifa de
    /// temporada vigente si existe para esa fecha, o la tarifa base con recargo de
    /// fin de semana en caso contrario.
    /// </summary>
    decimal CalculateTotalPrice(RoomType roomType, IReadOnlyCollection<SeasonalRate> seasonalRates, DateOnly checkIn, DateOnly checkOut);
}
