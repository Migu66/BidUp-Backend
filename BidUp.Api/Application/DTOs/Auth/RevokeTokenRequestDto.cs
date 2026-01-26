using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auth;

/// <summary>
/// DTO para solicitar la revocación de un refresh token
/// </summary>
public class RevokeTokenRequestDto
{
	[Required(ErrorMessage = "El token de refresco es requerido")]
	public string RefreshToken { get; set; } = string.Empty;
}
