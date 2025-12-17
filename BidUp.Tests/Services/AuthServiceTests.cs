using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BidUp.Api.Application.Services;
using BidUp.Api.Application.DTOs.Auth;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Exceptions;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Tests.Services;

public class AuthServiceTests
{
	private readonly Mock<UserManager<User>> _mockUserManager;
	private readonly Mock<IJwtService> _mockJwtService;
	private readonly ApplicationDbContext _context;
	private readonly AuthService _authService;

	public AuthServiceTests()
	{
		// Configurar UserManager mock
		var userStore = new Mock<IUserStore<User>>();
#pragma warning disable CS8625 // No se puede convertir un literal NULL en un tipo de referencia que no acepta valores NULL
		_mockUserManager = new Mock<UserManager<User>>(
			userStore.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

		// Configurar JwtService mock
		_mockJwtService = new Mock<IJwtService>();
		_mockJwtService.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
			.Returns("fake-access-token");
		_mockJwtService.Setup(x => x.GenerateRefreshToken())
			.Returns("fake-refresh-token");
		_mockJwtService.Setup(x => x.GetAccessTokenExpiration())
			.Returns(DateTime.UtcNow.AddMinutes(15));
		_mockJwtService.Setup(x => x.GetRefreshTokenExpiration())
			.Returns(DateTime.UtcNow.AddDays(7));

		// Configurar DbContext con InMemory
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;
		_context = new ApplicationDbContext(options);

		// Crear servicio
		_authService = new AuthService(
			_mockUserManager.Object,
			_mockJwtService.Object,
			_context);
	}

	#region Register Tests

	[Fact]
	public async Task RegisterAsync_WithValidData_ReturnsAuthResponse()
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

		_mockUserManager.Setup(x => x.FindByEmailAsync(registerDto.Email))
			.ReturnsAsync((User?)null);
		_mockUserManager.Setup(x => x.FindByNameAsync(registerDto.UserName))
			.ReturnsAsync((User?)null);
		_mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), registerDto.Password))
			.ReturnsAsync(IdentityResult.Success);

		// Act
		var result = await _authService.RegisterAsync(registerDto);

		// Assert
		result.Should().NotBeNull();
		result.Email.Should().Be(registerDto.Email);
		result.UserName.Should().Be(registerDto.UserName);
		result.AccessToken.Should().Be("fake-access-token");
		result.RefreshToken.Should().Be("fake-refresh-token");
		_mockUserManager.Verify(x => x.CreateAsync(It.IsAny<User>(), registerDto.Password), Times.Once);
	}

	[Fact]
	public async Task RegisterAsync_WithExistingEmail_ThrowsValidationException()
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

		var existingUser = new User { Email = registerDto.Email };
		_mockUserManager.Setup(x => x.FindByEmailAsync(registerDto.Email))
			.ReturnsAsync(existingUser);

		// Act
		Func<Task> act = async () => await _authService.RegisterAsync(registerDto);

		// Assert
		await act.Should().ThrowAsync<ValidationException>()
			.WithMessage("El email ya está registrado");
	}

	[Fact]
	public async Task RegisterAsync_WithExistingUsername_ThrowsValidationException()
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

		_mockUserManager.Setup(x => x.FindByEmailAsync(registerDto.Email))
			.ReturnsAsync((User?)null);
		
		var existingUser = new User { UserName = registerDto.UserName };
		_mockUserManager.Setup(x => x.FindByNameAsync(registerDto.UserName))
			.ReturnsAsync(existingUser);

		// Act
		Func<Task> act = async () => await _authService.RegisterAsync(registerDto);

		// Assert
		await act.Should().ThrowAsync<ValidationException>()
			.WithMessage("El nombre de usuario ya está en uso");
	}

	[Fact]
	public async Task RegisterAsync_WithIdentityErrors_ThrowsValidationException()
	{
		// Arrange
		var registerDto = new RegisterRequestDto
		{
			FirstName = "Juan",
			LastName = "Pérez",
			Email = "juan@example.com",
			UserName = "juanperez",
			Password = "weak",
			ConfirmPassword = "weak"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(registerDto.Email))
			.ReturnsAsync((User?)null);
		_mockUserManager.Setup(x => x.FindByNameAsync(registerDto.UserName))
			.ReturnsAsync((User?)null);

		var identityErrors = new[]
		{
			new IdentityError { Description = "La contraseña es muy débil" }
		};
		_mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), registerDto.Password))
			.ReturnsAsync(IdentityResult.Failed(identityErrors));

		// Act
		Func<Task> act = async () => await _authService.RegisterAsync(registerDto);

		// Assert
		await act.Should().ThrowAsync<ValidationException>();
	}

	#endregion

	#region Login Tests

	[Fact]
	public async Task LoginAsync_WithValidEmailCredentials_ReturnsAuthResponse()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			FirstName = "Juan",
			LastName = "Pérez",
			IsActive = true
		};

		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = user.Email,
			Password = "Password123!"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(loginDto.EmailOrUserName))
			.ReturnsAsync(user);
		_mockUserManager.Setup(x => x.CheckPasswordAsync(user, loginDto.Password))
			.ReturnsAsync(true);

		// Act
		var result = await _authService.LoginAsync(loginDto);

		// Assert
		result.Should().NotBeNull();
		result.Email.Should().Be(user.Email);
		result.UserName.Should().Be(user.UserName);
		result.AccessToken.Should().Be("fake-access-token");
		result.RefreshToken.Should().Be("fake-refresh-token");
	}

	[Fact]
	public async Task LoginAsync_WithValidUsernameCredentials_ReturnsAuthResponse()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			FirstName = "Juan",
			LastName = "Pérez",
			IsActive = true
		};

		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = user.UserName,
			Password = "Password123!"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(loginDto.EmailOrUserName))
			.ReturnsAsync((User?)null);
		_mockUserManager.Setup(x => x.FindByNameAsync(loginDto.EmailOrUserName))
			.ReturnsAsync(user);
		_mockUserManager.Setup(x => x.CheckPasswordAsync(user, loginDto.Password))
			.ReturnsAsync(true);

		// Act
		var result = await _authService.LoginAsync(loginDto);

		// Assert
		result.Should().NotBeNull();
		result.Email.Should().Be(user.Email);
		result.UserName.Should().Be(user.UserName);
	}

	[Fact]
	public async Task LoginAsync_WithInvalidUser_ThrowsAuthenticationException()
	{
		// Arrange
		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = "nonexistent@example.com",
			Password = "Password123!"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(loginDto.EmailOrUserName))
			.ReturnsAsync((User?)null);
		_mockUserManager.Setup(x => x.FindByNameAsync(loginDto.EmailOrUserName))
			.ReturnsAsync((User?)null);

		// Act
		Func<Task> act = async () => await _authService.LoginAsync(loginDto);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Credenciales inválidas");
	}

	[Fact]
	public async Task LoginAsync_WithInactiveUser_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = false
		};

		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = user.Email,
			Password = "Password123!"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(loginDto.EmailOrUserName))
			.ReturnsAsync(user);

		// Act
		Func<Task> act = async () => await _authService.LoginAsync(loginDto);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("La cuenta está desactivada");
	}

	[Fact]
	public async Task LoginAsync_WithInvalidPassword_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = true
		};

		var loginDto = new LoginRequestDto
		{
			EmailOrUserName = user.Email,
			Password = "WrongPassword!"
		};

		_mockUserManager.Setup(x => x.FindByEmailAsync(loginDto.EmailOrUserName))
			.ReturnsAsync(user);
		_mockUserManager.Setup(x => x.CheckPasswordAsync(user, loginDto.Password))
			.ReturnsAsync(false);

		// Act
		Func<Task> act = async () => await _authService.LoginAsync(loginDto);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Credenciales inválidas");
	}

	#endregion

	#region RefreshToken Tests

	[Fact]
	public async Task RefreshTokenAsync_WithValidToken_ReturnsNewAuthResponse()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			FirstName = "Juan",
			LastName = "Pérez",
			IsActive = true
		};

		var refreshToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "valid-refresh-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			RevokedAt = null
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(refreshToken);
		await _context.SaveChangesAsync();

		// Act
		var result = await _authService.RefreshTokenAsync(refreshToken.Token);

		// Assert
		result.Should().NotBeNull();
		result.AccessToken.Should().Be("fake-access-token");
		result.RefreshToken.Should().NotBe(refreshToken.Token);
		
		// Verificar que el token anterior fue revocado
		var oldToken = await _context.RefreshTokens.FindAsync(refreshToken.Id);
		oldToken!.RevokedAt.Should().NotBeNull();
		oldToken.ReasonRevoked.Should().Be("Reemplazado por nuevo token");
	}

	[Fact]
	public async Task RefreshTokenAsync_WithInvalidToken_ThrowsAuthenticationException()
	{
		// Arrange
		var invalidToken = "invalid-refresh-token";

		// Act
		Func<Task> act = async () => await _authService.RefreshTokenAsync(invalidToken);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Token de refresco inválido");
	}

	[Fact]
	public async Task RefreshTokenAsync_WithRevokedToken_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = true
		};

		var revokedToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "revoked-refresh-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow.AddDays(-8),
			ExpiresAt = DateTime.UtcNow.AddDays(-1),
			RevokedAt = DateTime.UtcNow.AddDays(-1),
			ReasonRevoked = "Revocado manualmente"
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(revokedToken);
		await _context.SaveChangesAsync();

		// Act
		Func<Task> act = async () => await _authService.RefreshTokenAsync(revokedToken.Token);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Token de refresco inválido o expirado");
		
		// Verificar que se revocaron todos los tokens del usuario
		var allTokens = await _context.RefreshTokens
			.Where(rt => rt.UserId == user.Id)
			.ToListAsync();
		allTokens.Should().OnlyContain(t => t.RevokedAt != null);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithExpiredToken_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = true
		};

		var expiredToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "expired-refresh-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow.AddDays(-8),
			ExpiresAt = DateTime.UtcNow.AddDays(-1),
			RevokedAt = null
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(expiredToken);
		await _context.SaveChangesAsync();

		// Act
		Func<Task> act = async () => await _authService.RefreshTokenAsync(expiredToken.Token);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Token de refresco inválido o expirado");
	}

	[Fact]
	public async Task RefreshTokenAsync_WithInactiveUser_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = false
		};

		var refreshToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "valid-refresh-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			RevokedAt = null
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(refreshToken);
		await _context.SaveChangesAsync();

		// Act
		Func<Task> act = async () => await _authService.RefreshTokenAsync(refreshToken.Token);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("La cuenta está desactivada");
	}

	#endregion

	#region RevokeToken Tests

	[Fact]
	public async Task RevokeTokenAsync_WithValidToken_RevokesToken()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = true
		};

		var refreshToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "valid-refresh-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			RevokedAt = null
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(refreshToken);
		await _context.SaveChangesAsync();

		// Act
		await _authService.RevokeTokenAsync(refreshToken.Token);

		// Assert
		var revokedToken = await _context.RefreshTokens.FindAsync(refreshToken.Id);
		revokedToken!.RevokedAt.Should().NotBeNull();
		revokedToken.ReasonRevoked.Should().Be("Revocado manualmente");
	}

	[Fact]
	public async Task RevokeTokenAsync_WithInvalidToken_ThrowsAuthenticationException()
	{
		// Arrange
		var invalidToken = "invalid-token";

		// Act
		Func<Task> act = async () => await _authService.RevokeTokenAsync(invalidToken);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Token de refresco inválido");
	}

	[Fact]
	public async Task RevokeTokenAsync_WithAlreadyRevokedToken_ThrowsAuthenticationException()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = "juan@example.com",
			UserName = "juanperez",
			IsActive = true
		};

		var revokedToken = new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = "revoked-token",
			UserId = user.Id,
			User = user,
			CreatedAt = DateTime.UtcNow.AddDays(-2),
			ExpiresAt = DateTime.UtcNow.AddDays(5),
			RevokedAt = DateTime.UtcNow.AddDays(-1),
			ReasonRevoked = "Ya revocado"
		};

		_context.Users.Add(user);
		_context.RefreshTokens.Add(revokedToken);
		await _context.SaveChangesAsync();

		// Act
		Func<Task> act = async () => await _authService.RevokeTokenAsync(revokedToken.Token);

		// Assert
		await act.Should().ThrowAsync<AuthenticationException>()
			.WithMessage("Token de refresco inválido");
	}

	#endregion
}
