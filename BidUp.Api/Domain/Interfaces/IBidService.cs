using BidUp.Api.Application.DTOs.Auction;

namespace BidUp.Api.Domain.Interfaces;

/// <summary>
/// Resultado de una operación de puja
/// </summary>
public class BidResult
{
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
	public BidDto? Bid { get; set; }
	public decimal? NewCurrentPrice { get; set; }
	public Guid? PreviousHighBidderId { get; set; } // Para notificar que fue superado

	public static BidResult Succeeded(BidDto bid, decimal newPrice, Guid? previousBidderId = null)
		=> new() { Success = true, Bid = bid, NewCurrentPrice = newPrice, PreviousHighBidderId = previousBidderId };

	public static BidResult Failed(string error)
		=> new() { Success = false, ErrorMessage = error };
}

public interface IBidService
{
	/// <summary>
	/// Coloca una puja con manejo de concurrencia usando Redis locks
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	/// <param name="bidderId">ID del usuario que puja</param>
	/// <param name="amount">Monto de la puja</param>
	/// <param name="ipAddress">IP del usuario para auditoría</param>
	/// <returns>Resultado de la operación</returns>
	Task<BidResult> PlaceBidAsync(Guid auctionId, Guid bidderId, decimal amount, string? ipAddress);

	/// <summary>
	/// Obtiene la puja más alta de una subasta
	/// </summary>
	Task<BidDto?> GetHighestBidAsync(Guid auctionId);

	/// <summary>
	/// Obtiene las pujas de un usuario
	/// </summary>
	Task<IEnumerable<BidDto>> GetUserBidsAsync(Guid userId, int page = 1, int pageSize = 20);
}
