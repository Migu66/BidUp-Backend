using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BidUp.Api.Application.DTOs.Auth;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Domain.Exceptions;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
	private readonly IAuthService _authService;

	public AuthController(IAuthService authService)
	{
		_authService = authService;
	}

	/// <summary>
	/// Registra un nuevo usuario
	/// </summary>
	[HttpPost("register")]
	[ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
	{
		if (!ModelState.IsValid)
		{
			var errors = ModelState.Values
				.SelectMany(v => v.Errors)
				.Select(e => e.ErrorMessage)
				.ToList();
			return BadRequest(ApiResponseDto.ErrorResponse("Datos de registro inválidos", errors));
		}

		try
		{
			var result = await _authService.RegisterAsync(request);
			return CreatedAtAction(
				nameof(Register),
				ApiResponseDto<AuthResponseDto>.SuccessResponse(result, "Usuario registrado exitosamente"));
		}
		catch (ValidationException ex)
		{
			return BadRequest(ApiResponseDto.ErrorResponse("Error de validación", ex.Errors));
		}
	}

	/// <summary>
	/// Inicia sesión y obtiene tokens de acceso
	/// </summary>
	[HttpPost("login")]
	[ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
	{
		if (!ModelState.IsValid)
		{
			var errors = ModelState.Values
				.SelectMany(v => v.Errors)
				.Select(e => e.ErrorMessage)
				.ToList();
			return BadRequest(ApiResponseDto.ErrorResponse("Datos de inicio de sesión inválidos", errors));
		}

		try
		{
			var result = await _authService.LoginAsync(request);
			return Ok(ApiResponseDto<AuthResponseDto>.SuccessResponse(result, "Inicio de sesión exitoso"));
		}
		catch (AuthenticationException ex)
		{
			return Unauthorized(ApiResponseDto.ErrorResponse(ex.Message));
		}
	}

	/// <summary>
	/// Renueva el token de acceso usando el refresh token
	/// </summary>
	[HttpPost("refresh-token")]
	[ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ApiResponseDto.ErrorResponse("Token de refresco requerido"));
		}

		try
		{
			var result = await _authService.RefreshTokenAsync(request.RefreshToken);
			return Ok(ApiResponseDto<AuthResponseDto>.SuccessResponse(result, "Token renovado exitosamente"));
		}
		catch (AuthenticationException ex)
		{
			return Unauthorized(ApiResponseDto.ErrorResponse(ex.Message));
		}
	}

	/// <summary>
	/// Cierra la sesión revocando el refresh token
	/// </summary>
	[HttpPost("logout")]
	[Authorize]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
	{
		try
		{
			await _authService.RevokeTokenAsync(request.RefreshToken);
			return Ok(ApiResponseDto.SuccessResponse("Sesión cerrada exitosamente"));
		}
		catch (AuthenticationException ex)
		{
			return Unauthorized(ApiResponseDto.ErrorResponse(ex.Message));
		}
	}

	/// <summary>
	/// Endpoint protegido de ejemplo - Obtiene información del usuario actual
	/// </summary>
	[HttpGet("me")]
	[Authorize]
	[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public IActionResult GetCurrentUser()
	{
		var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
					 ?? User.FindFirst("sub")?.Value;
		var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
					?? User.FindFirst("email")?.Value;
		var username = User.FindFirst("username")?.Value;
		var fullname = User.FindFirst("fullname")?.Value;

		var userInfo = new
		{
			UserId = userId,
			Email = email,
			UserName = username,
			FullName = fullname
		};

		return Ok(ApiResponseDto<object>.SuccessResponse(userInfo, "Usuario autenticado"));
	}
}
