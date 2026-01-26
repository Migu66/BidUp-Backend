namespace BidUp.Api.Domain.Entities;

public class Bid
{
	public Guid Id { get; set; }

	// Monto de la puja
	public decimal Amount { get; set; }

	/// <summary>
	/// Timestamp preciso para resolver conflictos de pujas simultáneas.
	/// Se usa DateTime con precisión de ticks para ordenar por tiempo exacto.
	/// </summary>
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Indica si esta puja es la ganadora de la subasta
	/// </summary>
	public bool IsWinning { get; set; } = false;

	/// <summary>
	/// Indica si la puja fue automática (auto-bid)
	/// </summary>
	public bool IsAutoBid { get; set; } = false;

	/// <summary>
	/// IP del usuario para rate limiting y auditoría
	/// </summary>
	public string? IpAddress { get; set; }

	// Relaciones
	public Guid AuctionId { get; set; }
	public Auction Auction { get; set; } = null!;

	public Guid BidderId { get; set; }
	public User Bidder { get; set; } = null!;
}
