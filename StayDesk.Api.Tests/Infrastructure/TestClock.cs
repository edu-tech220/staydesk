using StayDesk.Api.Common;

namespace StayDesk.Api.Tests.Infrastructure;

/// <summary>Reloj mutable para probar deterministicamente reglas que dependen del tiempo (no-show, politica de cancelacion).</summary>
public class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
