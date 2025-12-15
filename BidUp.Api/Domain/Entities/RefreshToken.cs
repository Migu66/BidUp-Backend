namespace BidUp.Api.Domain.Entities;

public class RefreshToken
{
	public Guid Id { get; set; }
	public string Token { get; set; } = string.Empty;
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? RevokedAt { get; set; }
	public string? ReplacedByToken { get; set; }
	public string? ReasonRevoked { get; set; }

	public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
	public bool IsRevoked => RevokedAt != null;
	public bool IsActive => !IsRevoked && !IsExpired;
}
