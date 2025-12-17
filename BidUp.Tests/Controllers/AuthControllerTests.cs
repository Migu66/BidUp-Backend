using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using BidUp.Api.Controllers;
using BidUp.Api.Application.DTOs.Auth;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Domain.Exceptions;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Tests.Controllers;

public class AuthControllerTests
{
	private readonly Mock<IAuthService> _mockAuthService;
	private readonly AuthController _controller;

	public AuthControllerTests()
	{
		_mockAuthService = new Mock<IAuthService>();
		_controller = new AuthController(_mockAuthService.Object);
	}

	#region Register Tests

	[Fact]
	public async Task Register_WithValidData_ReturnsCreatedResult()
	{
		// Arrange
		var registerDto = new RegisterRequestDto
		{
			FirstName = "Juan",
			LastName = "Pérez",
			Email = "juan@example.com",
			UserName = "juanperez",
			Password = "Password123!",
			ConfirmPassword = "Password123!"
		};

		var authResponse = new AuthResponseDto
		{
			UserId = Guid.NewGuid(),
			Email = registerDto.Email,
			UserName = registerDto.UserName,
			FullName = $"{registerDto.FirstName} {registerDto.LastName}",
			AccessToken = "fake-access-token",
			RefreshToken = "fake-refresh-token",
			AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
			RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
		};

		_mockAuthService.Setup(x => x.RegisterAsync(registerDto))
			.ReturnsAsync(authResponse);

		// Act
		var result = await _controller.Register(registerDto);

		// Assert
		var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<AuthResponseDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Usuario registrado exitosamente");
		response.Data.Should().NotBeNull();
		response.Data!.Email.Should().Be(registerDto.Email);
		response.Data.UserName.Should().Be(registerDto.UserName);
		
		_mockAuthService.Verify(x => x.RegisterAsync(registerDto), Times.Once);
	}

	[Fact]
	public async Task Register_WithInvalidModelState_ReturnsBadRequest()
	{
		// Arrange
		var registerDto = new RegisterRequestDto
		{
			FirstName = "Juan",
			LastName = "Pérez",
			Email = "invalid-email",
			UserName = "juanperez",
			Password = "Password123!",
			ConfirmPassword = "Password123!"
		};

		_controller.ModelState.AddModelError("Email", "El formato del email no es válido");

		// Act
		var result = await _controller.Register(registerDto);

		// Assert
		var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Datos de registro inválidos");
		response.Errors.Should().NotBeEmpty();
		
		_mockAuthService.Verify(x => x.RegisterAsync(It.IsAny<RegisterRequestDto>()), Times.Never);
	}

	[Fact]
	public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
	{
		// Arrange
		var registerDto = new RegisterRequestDto
		{
			FirstName = "Juan",
			LastName = "Pérez",
			Email = "existing@example.com",
			UserName = "juanperez",
			Password = "Password123!",
			ConfirmPassword = "Password123!"
		};

		_mockAuthService.Setup(x => x.RegisterAsync(registerDto))
			.ThrowsAsync(new ValidationException("El email ya está registrado"));

		// Act
		var result = await _controller.Register(registerDto);

		// Assert
		var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Error de validación");
		response.Errors.Should().Contain("El email ya está registrado");
		
		_mockAuthService.Verify(x => x.RegisterAsync(registerDto), Times.Once);
	}

	[Fact]
	public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
	{
		// Arrange
		var registerDto = new RegisterRequestDto
		{
			FirstName = "Juan",
			LastName = "Pérez",
			Email = "juan@example.com",
			UserName = "existinguser",
			Password = "Password123!",
			ConfirmPassword = "Password123!"
		};

		_mockAuthService.Setup(x => x.RegisterAsync(registerDto))
			.ThrowsAsync(new ValidationException("El nombre de usuario ya está en uso"));

		// Act
		var result = await _controller.Register(registerDto);

		// Assert
		var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Error de validación");
		response.Errors.Should().Contain("El nombre de usuario ya está en uso");
	}

	#endregion

	#region Login Tests

	[Fact]
	public async Task Login_WithValidCredentials_ReturnsOkResult()
	{
		// Arrange
		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = "juan@example.com",
			Password = "Password123!"
		};

		var authResponse = new AuthResponseDto
		{
			UserId = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			FullName = "Juan Pérez",
			AccessToken = "fake-access-token",
			RefreshToken = "fake-refresh-token",
			AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
			RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
		};

		_mockAuthService.Setup(x => x.LoginAsync(loginDto))
			.ReturnsAsync(authResponse);

		// Act
		var result = await _controller.Login(loginDto);

		// Assert
		var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<AuthResponseDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Inicio de sesión exitoso");
		response.Data.Should().NotBeNull();
		response.Data!.Email.Should().Be(authResponse.Email);
		response.Data.AccessToken.Should().Be("fake-access-token");
		
		_mockAuthService.Verify(x => x.LoginAsync(loginDto), Times.Once);
	}

	[Fact]
	public async Task Login_WithInvalidModelState_ReturnsBadRequest()
	{
		// Arrange
		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = "",
			Password = "Password123!"
		};

		_controller.ModelState.AddModelError("EmailOrUserName", "El email o nombre de usuario es requerido");

		// Act
		var result = await _controller.Login(loginDto);

		// Assert
		var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Datos de inicio de sesión inválidos");
		
		_mockAuthService.Verify(x => x.LoginAsync(It.IsAny<LoginRequestDto>()), Times.Never);
	}

	[Fact]
	public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
	{
		// Arrange
		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = "juan@example.com",
			Password = "WrongPassword!"
		};

		_mockAuthService.Setup(x => x.LoginAsync(loginDto))
			.ThrowsAsync(new AuthenticationException("Credenciales inválidas"));

		// Act
		var result = await _controller.Login(loginDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Credenciales inválidas");
		
		_mockAuthService.Verify(x => x.LoginAsync(loginDto), Times.Once);
	}

	[Fact]
	public async Task Login_WithInactiveUser_ReturnsUnauthorized()
	{
		// Arrange
		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = "juan@example.com",
			Password = "Password123!"
		};

		_mockAuthService.Setup(x => x.LoginAsync(loginDto))
			.ThrowsAsync(new AuthenticationException("La cuenta está desactivada"));

		// Act
		var result = await _controller.Login(loginDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("La cuenta está desactivada");
	}

	#endregion

	#region RefreshToken Tests

	[Fact]
	public async Task RefreshToken_WithValidToken_ReturnsOkResult()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "valid-refresh-token"
		};

		var authResponse = new AuthResponseDto
		{
			UserId = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			FullName = "Juan Pérez",
			AccessToken = "new-access-token",
			RefreshToken = "new-refresh-token",
			AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
			RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
		};

		_mockAuthService.Setup(x => x.RefreshTokenAsync(refreshTokenDto.RefreshToken))
			.ReturnsAsync(authResponse);

		// Act
		var result = await _controller.RefreshToken(refreshTokenDto);

		// Assert
		var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<AuthResponseDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Token renovado exitosamente");
		response.Data.Should().NotBeNull();
		response.Data!.AccessToken.Should().Be("new-access-token");
		response.Data.RefreshToken.Should().Be("new-refresh-token");
		
		_mockAuthService.Verify(x => x.RefreshTokenAsync(refreshTokenDto.RefreshToken), Times.Once);
	}

	[Fact]
	public async Task RefreshToken_WithInvalidModelState_ReturnsBadRequest()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = ""
		};

		_controller.ModelState.AddModelError("RefreshToken", "El token de refresco es requerido");

		// Act
		var result = await _controller.RefreshToken(refreshTokenDto);

		// Assert
		var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Token de refresco requerido");
		
		_mockAuthService.Verify(x => x.RefreshTokenAsync(It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "invalid-refresh-token"
		};

		_mockAuthService.Setup(x => x.RefreshTokenAsync(refreshTokenDto.RefreshToken))
			.ThrowsAsync(new AuthenticationException("Token de refresco inválido"));

		// Act
		var result = await _controller.RefreshToken(refreshTokenDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Token de refresco inválido");
		
		_mockAuthService.Verify(x => x.RefreshTokenAsync(refreshTokenDto.RefreshToken), Times.Once);
	}

	[Fact]
	public async Task RefreshToken_WithExpiredToken_ReturnsUnauthorized()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "expired-refresh-token"
		};

		_mockAuthService.Setup(x => x.RefreshTokenAsync(refreshTokenDto.RefreshToken))
			.ThrowsAsync(new AuthenticationException("Token de refresco inválido o expirado"));

		// Act
		var result = await _controller.RefreshToken(refreshTokenDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Token de refresco inválido o expirado");
	}

	#endregion

	#region Logout Tests

	[Fact]
	public async Task Logout_WithValidToken_ReturnsOkResult()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "valid-refresh-token"
		};

		_mockAuthService.Setup(x => x.RevokeTokenAsync(refreshTokenDto.RefreshToken))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _controller.Logout(refreshTokenDto);

		// Assert
		var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Sesión cerrada exitosamente");
		
		_mockAuthService.Verify(x => x.RevokeTokenAsync(refreshTokenDto.RefreshToken), Times.Once);
	}

	[Fact]
	public async Task Logout_WithInvalidToken_ReturnsUnauthorized()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "invalid-refresh-token"
		};

		_mockAuthService.Setup(x => x.RevokeTokenAsync(refreshTokenDto.RefreshToken))
			.ThrowsAsync(new AuthenticationException("Token de refresco inválido"));

		// Act
		var result = await _controller.Logout(refreshTokenDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Token de refresco inválido");
		
		_mockAuthService.Verify(x => x.RevokeTokenAsync(refreshTokenDto.RefreshToken), Times.Once);
	}

	[Fact]
	public async Task Logout_WithAlreadyRevokedToken_ReturnsUnauthorized()
	{
		// Arrange
		var refreshTokenDto = new RefreshTokenRequestDto
		{
			RefreshToken = "revoked-refresh-token"
		};

		_mockAuthService.Setup(x => x.RevokeTokenAsync(refreshTokenDto.RefreshToken))
			.ThrowsAsync(new AuthenticationException("Token de refresco inválido"));

		// Act
		var result = await _controller.Logout(refreshTokenDto);

		// Assert
		var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Token de refresco inválido");
	}

	#endregion
}
