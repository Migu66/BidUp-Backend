using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auth;

public class RefreshTokenRequestDto
{
	[Required(ErrorMessage = "El token de refresco es requerido")]
	public string RefreshToken { get; set; } = string.Empty;
}
