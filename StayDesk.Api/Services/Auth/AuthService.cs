using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayDesk.Api.Common;
using StayDesk.Api.Common.Exceptions;
using StayDesk.Api.Data;
using StayDesk.Api.Domain.Entities;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Auth;

namespace StayDesk.Api.Services.Auth;

public class AuthService : IAuthService
{
    private readonly StayDeskDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(StayDeskDbContext db, IJwtTokenService jwtTokenService, IClock clock, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var alreadyExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Email == normalizedEmail, ct);
        if (alreadyExists)
        {
            throw new ConflictException("Ya existe una cuenta registrada con ese correo.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Role = UserRole.Guest,
            CreatedAtUtc = _clock.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct)
            ?? throw new ValidationFailedException("Correo o contrasena invalidos.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ValidationFailedException("Correo o contrasena invalidos.");
        }

        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = _jwtTokenService.GenerateToken(user);
        var expiresAtUtc = _clock.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);
        return new AuthResponse(token, user.FullName, user.Email, user.Role.ToString(), expiresAtUtc);
    }
}
