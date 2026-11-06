namespace StayDesk.Api.Domain.Enums;

/// <summary>
/// Pending -> Confirmed -> CheckedIn -> CheckedOut es el camino feliz.
/// Pending/Confirmed -> Cancelled y Confirmed -> NoShow son las salidas alternas.
/// </summary>
public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    CheckedIn = 2,
    CheckedOut = 3,
    Cancelled = 4,
    NoShow = 5
}
