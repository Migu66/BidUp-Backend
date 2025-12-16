namespace BidUp.Api.Application.DTOs.Auction;

public class BidDto
{
	public Guid Id { get; set; }
	public decimal Amount { get; set; }
	public DateTime Timestamp { get; set; }
	public bool IsWinning { get; set; }

	// Información del postor
	public Guid BidderId { get; set; }
	public string BidderName { get; set; } = string.Empty;

	// Información de la subasta
	public Guid AuctionId { get; set; }
}
