using StayDesk.Api.Domain.Enums;

namespace StayDesk.Api.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Guest;
    public DateTime CreatedAtUtc { get; set; }

    public List<Reservation> Reservations { get; set; } = new();
}
