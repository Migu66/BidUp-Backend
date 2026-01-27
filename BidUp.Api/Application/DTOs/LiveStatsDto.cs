namespace BidUp.Api.Application.DTOs;

public class LiveStatsDto
{
	public int ActiveAuctions { get; set; }
	public int ConnectedUsers { get; set; }
	public DateTime Timestamp { get; set; }
}
