namespace BidUp.Api.Application.DTOs.Auction;

public class AuctionDto
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? ImageUrl { get; set; }
	public decimal StartingPrice { get; set; }
	public decimal CurrentPrice { get; set; }
	public decimal MinBidIncrement { get; set; }
	public DateTime StartTime { get; set; }
	public DateTime EndTime { get; set; }
	public string Status { get; set; } = string.Empty;
	public int TotalBids { get; set; }
	public TimeSpan TimeRemaining { get; set; }

	// Información del vendedor
	public Guid SellerId { get; set; }
	public string SellerName { get; set; } = string.Empty;

	// Categoría
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;

	// Última puja
	public BidDto? LatestBid { get; set; }
}
