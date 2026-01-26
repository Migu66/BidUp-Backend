using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BidUp.Api.Application.DTOs.Auth;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Exceptions;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Api.Application.Services;

public class AuthService : IAuthService
{
	private readonly UserManager<User> _userManager;
	private readonly IJwtService _jwtService;
	private readonly ApplicationDbContext _context;

	public AuthService(
		UserManager<User> userManager,
		IJwtService jwtService,
		ApplicationDbContext context)
	{
		_userManager = userManager;
		_jwtService = jwtService;
		_context = context;
	}

	public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
	{
		// Verificar si el email ya existe
		var existingUserByEmail = await _userManager.FindByEmailAsync(request.Email);
		if (existingUserByEmail != null)
		{
			throw new ValidationException("El email ya está registrado");
		}

		// Verificar si el username ya existe
		var existingUserByUsername = await _userManager.FindByNameAsync(request.UserName);
		if (existingUserByUsername != null)
		{
			throw new ValidationException("El nombre de usuario ya está en uso");
		}

		var user = new User
		{
			FirstName = request.FirstName,
			LastName = request.LastName,
			Email = request.Email,
			UserName = request.UserName,
			EmailConfirmed = true, // Por ahora lo dejamos confirmado, luego puedes agregar verificación por email
			IsActive = true
		};

		var result = await _userManager.CreateAsync(user, request.Password);

		if (!result.Succeeded)
		{
			var errors = result.Errors.Select(e => e.Description).ToList();
			throw new ValidationException(errors);
		}

		return await GenerateAuthResponseAsync(user);
	}

	public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
	{
		// Buscar usuario por email o username
		var user = await _userManager.FindByEmailAsync(request.EmailOrUserName)
				   ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

		if (user == null)
		{
			throw new AuthenticationException("Credenciales inválidas");
		}

		if (!user.IsActive)
		{
			throw new AuthenticationException("La cuenta está desactivada");
		}

		var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
		if (!isPasswordValid)
		{
			throw new AuthenticationException("Credenciales inválidas");
		}

		// Revocar todos los refresh tokens anteriores del usuario
		await RevokeAllUserTokensAsync(user.Id, "Login con nuevas credenciales");

		return await GenerateAuthResponseAsync(user);
	}

	public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
	{
		var storedToken = await _context.RefreshTokens
			.Include(rt => rt.User)
			.FirstOrDefaultAsync(rt => rt.Token == refreshToken);

		if (storedToken == null)
		{
			throw new AuthenticationException("Token de refresco inválido");
		}

		if (!storedToken.IsActive)
		{
			// Si alguien intenta usar un token revocado o expirado, revocar todos los tokens del usuario
			if (storedToken.IsRevoked)
			{
				await RevokeAllUserTokensAsync(storedToken.UserId, "Intento de reutilización de token revocado");
			}
			throw new AuthenticationException("Token de refresco inválido o expirado");
		}

		var user = storedToken.User;
		if (!user.IsActive)
		{
			throw new AuthenticationException("La cuenta está desactivada");
		}

		// Revocar el token actual
		storedToken.RevokedAt = DateTime.UtcNow;
		storedToken.ReasonRevoked = "Reemplazado por nuevo token";

		// Generar nuevo refresh token
		var newRefreshToken = CreateRefreshToken(user.Id);
		storedToken.ReplacedByToken = newRefreshToken.Token;

		_context.RefreshTokens.Add(newRefreshToken);
		await _context.SaveChangesAsync();

		return new AuthResponseDto
		{
			UserId = user.Id,
			Email = user.Email ?? string.Empty,
			UserName = user.UserName ?? string.Empty,
			FullName = user.FullName,
			AccessToken = _jwtService.GenerateAccessToken(user),
			RefreshToken = newRefreshToken.Token,
			AccessTokenExpiration = _jwtService.GetAccessTokenExpiration(),
			RefreshTokenExpiration = newRefreshToken.ExpiresAt
		};
	}

	public async Task<TokenResponseDto> RefreshAsync(string refreshToken)
	{
		var storedToken = await _context.RefreshTokens
			.Include(rt => rt.User)
			.FirstOrDefaultAsync(rt => rt.Token == refreshToken);

		if (storedToken == null)
		{
			throw new AuthenticationException("Token de refresco inválido");
		}

		if (!storedToken.IsActive)
		{
			// Si alguien intenta usar un token revocado o expirado, revocar todos los tokens del usuario
			if (storedToken.IsRevoked)
			{
				await RevokeAllUserTokensAsync(storedToken.UserId, "Intento de reutilización de token revocado");
			}
			throw new AuthenticationException("Token de refresco inválido o expirado");
		}

		var user = storedToken.User;
		if (!user.IsActive)
		{
			throw new AuthenticationException("La cuenta está desactivada");
		}

		// Revocar el token actual
		storedToken.RevokedAt = DateTime.UtcNow;
		storedToken.ReasonRevoked = "Reemplazado por nuevo token";

		// Generar nuevo refresh token
		var newRefreshToken = CreateRefreshToken(user.Id);
		storedToken.ReplacedByToken = newRefreshToken.Token;

		_context.RefreshTokens.Add(newRefreshToken);
		await _context.SaveChangesAsync();

		return new TokenResponseDto
		{
			AccessToken = _jwtService.GenerateAccessToken(user),
			RefreshToken = newRefreshToken.Token,
			ExpiresIn = _jwtService.GetAccessTokenExpirationSeconds()
		};
	}

	public async Task RevokeTokenAsync(string refreshToken)
	{
		var storedToken = await _context.RefreshTokens
			.FirstOrDefaultAsync(rt => rt.Token == refreshToken);

		if (storedToken == null || !storedToken.IsActive)
		{
			throw new AuthenticationException("Token de refresco inválido");
		}

		storedToken.RevokedAt = DateTime.UtcNow;
		storedToken.ReasonRevoked = "Revocado manualmente";
		await _context.SaveChangesAsync();
	}

	public async Task<bool> ValidateTokenAsync(string token)
	{
		var principal = _jwtService.GetPrincipalFromExpiredToken(token);
		return principal != null && await Task.FromResult(true);
	}

	private async Task<AuthResponseDto> GenerateAuthResponseAsync(User user)
	{
		var accessToken = _jwtService.GenerateAccessToken(user);
		var refreshToken = CreateRefreshToken(user.Id);

		_context.RefreshTokens.Add(refreshToken);
		await _context.SaveChangesAsync();

		return new AuthResponseDto
		{
			UserId = user.Id,
			Email = user.Email ?? string.Empty,
			UserName = user.UserName ?? string.Empty,
			FullName = user.FullName,
			AccessToken = accessToken,
			RefreshToken = refreshToken.Token,
			AccessTokenExpiration = _jwtService.GetAccessTokenExpiration(),
			RefreshTokenExpiration = refreshToken.ExpiresAt
		};
	}

	private RefreshToken CreateRefreshToken(Guid userId)
	{
		return new RefreshToken
		{
			Id = Guid.NewGuid(),
			Token = _jwtService.GenerateRefreshToken(),
			UserId = userId,
			ExpiresAt = _jwtService.GetRefreshTokenExpiration(),
			CreatedAt = DateTime.UtcNow
		};
	}

	private async Task RevokeAllUserTokensAsync(Guid userId, string reason)
	{
		var activeTokens = await _context.RefreshTokens
			.Where(rt => rt.UserId == userId && rt.RevokedAt == null)
			.ToListAsync();

		foreach (var token in activeTokens)
		{
			token.RevokedAt = DateTime.UtcNow;
			token.ReasonRevoked = reason;
		}

		await _context.SaveChangesAsync();
	}
}
