namespace StayDesk.Api.Common;

/// <summary>Abstrae la fecha/hora actual para que las reglas de negocio (politica de cancelacion, check-in) sean deterministas en pruebas.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
