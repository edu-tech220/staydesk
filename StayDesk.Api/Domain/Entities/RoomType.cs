namespace StayDesk.Api.Domain.Entities;

public class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int TotalRooms { get; set; }
    public decimal BasePricePerNight { get; set; }

    /// <summary>Recargo porcentual aplicado a noches de viernes y sabado (p. ej. 20 = +20%).</summary>
    public decimal WeekendSurchargePercent { get; set; }

    public List<SeasonalRate> SeasonalRates { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
}
