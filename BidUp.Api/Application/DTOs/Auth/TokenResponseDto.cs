namespace BidUp.Api.Application.DTOs.Auth;

/// <summary>
/// DTO simplificado para respuestas de refresh token
/// </summary>
public class TokenResponseDto
{
	public string AccessToken { get; set; } = string.Empty;
	public string RefreshToken { get; set; } = string.Empty;

	/// <summary>
	/// Tiempo de expiración del access token en segundos
	/// </summary>
	public int ExpiresIn { get; set; }
}
