namespace StayDesk.Api.Domain.Entities;

/// <summary>
/// Contador de habitaciones ocupadas de un tipo de habitacion para un dia especifico.
/// Es la base del control de sobreventa: una fila por (RoomTypeId, Date) que se
/// incrementa/decrementa de forma atomica al crear o cancelar una reserva.
/// </summary>
public class RoomTypeDailyInventory
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public DateOnly Date { get; set; }
    public int RoomsBooked { get; set; }
}
