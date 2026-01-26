using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auth;

public class LoginRequestDto
{
	[Required(ErrorMessage = "El email o nombre de usuario es requerido")]
	public string EmailOrUserName { get; set; } = string.Empty;

	[Required(ErrorMessage = "La contraseña es requerida")]
	public string Password { get; set; } = string.Empty;
}
