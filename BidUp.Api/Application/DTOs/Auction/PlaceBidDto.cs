using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auction;

public class PlaceBidDto
{
	[Required(ErrorMessage = "El ID de la subasta es requerido")]
	public Guid AuctionId { get; set; }

	[Required(ErrorMessage = "El monto de la puja es requerido")]
	[Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
	public decimal Amount { get; set; }
}
