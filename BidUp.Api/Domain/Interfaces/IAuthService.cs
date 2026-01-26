using BidUp.Api.Application.DTOs.Auth;

namespace BidUp.Api.Domain.Interfaces;

public interface IAuthService
{
	Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
	Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
	Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
	Task<TokenResponseDto> RefreshAsync(string refreshToken);
	Task RevokeTokenAsync(string refreshToken);
	Task<bool> ValidateTokenAsync(string token);
}
