namespace StayDesk.Api.Services.Reservations;

/// <summary>
/// Politica de penalizacion por cancelacion:
/// - 3 o mas dias antes del check-in: sin penalizacion.
/// - 1 o 2 dias antes: 50% del total.
/// - El mismo dia del check-in o despues: 100% del total (sin reembolso).
/// </summary>
public static class CancellationPolicy
{
    public static decimal CalculatePenalty(decimal totalPrice, DateOnly checkIn, DateOnly today)
    {
        var daysUntilCheckIn = checkIn.DayNumber - today.DayNumber;

        if (daysUntilCheckIn >= 3)
        {
            return 0m;
        }

        if (daysUntilCheckIn >= 1)
        {
            return decimal.Round(totalPrice * 0.5m, 2);
        }

        return totalPrice;
    }
}
