using StayDesk.Api.Domain.Entities;

namespace StayDesk.Api.Services.Auth;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
