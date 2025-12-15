using Microsoft.AspNetCore.Identity;

namespace BidUp.Api.Domain.Entities;

public class User : IdentityUser<Guid>
{
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
	public bool IsActive { get; set; } = true;

	public string FullName => $"{FirstName} {LastName}";
}
