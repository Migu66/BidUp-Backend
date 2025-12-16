namespace BidUp.Api.Application.DTOs.Auction;

/// <summary>
/// DTO para notificar una nueva puja en tiempo real
/// </summary>
public class BidNotificationDto
{
	public Guid AuctionId { get; set; }
	public BidDto Bid { get; set; } = null!;
	public decimal NewCurrentPrice { get; set; }
	public int TotalBids { get; set; }
	public TimeSpan TimeRemaining { get; set; }
}

/// <summary>
/// DTO para notificar cambio de estado de la subasta
/// </summary>
public class AuctionStatusNotificationDto
{
	public Guid AuctionId { get; set; }
	public string Status { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public BidDto? WinnerBid { get; set; }
}

/// <summary>
/// DTO para sincronizar el timer de la subasta
/// </summary>
public class AuctionTimerSyncDto
{
	public Guid AuctionId { get; set; }
	public DateTime EndTime { get; set; }
	public TimeSpan TimeRemaining { get; set; }
	public DateTime ServerTime { get; set; }
}

/// <summary>
/// DTO para notificar que el usuario ha sido superado
/// </summary>
public class OutbidNotificationDto
{
	public Guid AuctionId { get; set; }
	public string AuctionTitle { get; set; } = string.Empty;
	public decimal YourBid { get; set; }
	public decimal NewHighestBid { get; set; }
	public decimal MinimumNextBid { get; set; }
}
