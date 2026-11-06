using System.ComponentModel.DataAnnotations;

namespace StayDesk.Api.Dtos.Auth;

public record RegisterRequest(
    [Required, MaxLength(200)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record AuthResponse(string Token, string FullName, string Email, string Role, DateTime ExpiresAtUtc);
