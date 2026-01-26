using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auth;

public class RegisterRequestDto
{
	[Required(ErrorMessage = "El nombre es requerido")]
	[StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
	public string FirstName { get; set; } = string.Empty;

	[Required(ErrorMessage = "El apellido es requerido")]
	[StringLength(50, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 50 caracteres")]
	public string LastName { get; set; } = string.Empty;

	[Required(ErrorMessage = "El email es requerido")]
	[EmailAddress(ErrorMessage = "El formato del email no es válido")]
	public string Email { get; set; } = string.Empty;

	[Required(ErrorMessage = "El nombre de usuario es requerido")]
	[StringLength(30, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 30 caracteres")]
	[RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "El nombre de usuario solo puede contener letras, números y guiones bajos")]
	public string UserName { get; set; } = string.Empty;

	[Required(ErrorMessage = "La contraseña es requerida")]
	[StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
	[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&\-_.]{8,}$",
		ErrorMessage = "La contraseña debe contener al menos una mayúscula, una minúscula y un número")]
	public string Password { get; set; } = string.Empty;

	[Required(ErrorMessage = "La confirmación de contraseña es requerida")]
	[Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
	public string ConfirmPassword { get; set; } = string.Empty;
}
