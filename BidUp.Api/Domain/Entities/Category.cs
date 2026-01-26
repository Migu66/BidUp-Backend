namespace BidUp.Api.Domain.Entities;

public class Category
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ImageUrl { get; set; }
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	// Navegación
	public ICollection<Auction> Auctions { get; set; } = new List<Auction>();
}
