using System.Security.Claims;
using BidUp.Api.Domain.Entities;

namespace BidUp.Api.Domain.Interfaces;

public interface IJwtService
{
	string GenerateAccessToken(User user);
	string GenerateRefreshToken();
	ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
	DateTime GetAccessTokenExpiration();
	DateTime GetRefreshTokenExpiration();
}
