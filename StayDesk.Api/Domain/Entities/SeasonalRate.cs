namespace StayDesk.Api.Domain.Entities;

/// <summary>Tarifa fija por noche que reemplaza la tarifa base + recargo de fin de semana en un rango de fechas.</summary>
public class SeasonalRate
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal PricePerNight { get; set; }
}
