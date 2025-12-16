using BidUp.Api.Domain.Enums;

namespace BidUp.Api.Domain.Entities;

public class Auction
{
	public Guid Id { get; set; }

	// Información del producto
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? ImageUrl { get; set; }

	// Precios
	public decimal StartingPrice { get; set; }
	public decimal CurrentPrice { get; set; }
	public decimal? ReservePrice { get; set; } // Precio mínimo oculto
	public decimal MinBidIncrement { get; set; } = 1.00m; // Incremento mínimo por puja

	// Tiempos
	public DateTime StartTime { get; set; }
	public DateTime EndTime { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	// Estado
	public AuctionStatus Status { get; set; } = AuctionStatus.Pending;

	// Relaciones
	public Guid SellerId { get; set; }
	public User Seller { get; set; } = null!;

	public Guid CategoryId { get; set; }
	public Category Category { get; set; } = null!;

	public Guid? WinnerBidId { get; set; }
	public Bid? WinnerBid { get; set; }

	// Navegación
	public ICollection<Bid> Bids { get; set; } = new List<Bid>();

	// Propiedades calculadas
	public bool IsActive => Status == AuctionStatus.Active && DateTime.UtcNow < EndTime;
	public bool HasEnded => DateTime.UtcNow >= EndTime;
	public TimeSpan TimeRemaining => EndTime > DateTime.UtcNow ? EndTime - DateTime.UtcNow : TimeSpan.Zero;
	public int TotalBids => Bids.Count;
}
