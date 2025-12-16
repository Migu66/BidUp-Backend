using System.ComponentModel.DataAnnotations;

namespace BidUp.Api.Application.DTOs.Auction;

public class CreateAuctionDto
{
	[Required(ErrorMessage = "El título es requerido")]
	[StringLength(200, MinimumLength = 5, ErrorMessage = "El título debe tener entre 5 y 200 caracteres")]
	public string Title { get; set; } = string.Empty;

	[Required(ErrorMessage = "La descripción es requerida")]
	[StringLength(2000, MinimumLength = 20, ErrorMessage = "La descripción debe tener entre 20 y 2000 caracteres")]
	public string Description { get; set; } = string.Empty;

	public string? ImageUrl { get; set; }

	[Required(ErrorMessage = "El precio inicial es requerido")]
	[Range(0.01, double.MaxValue, ErrorMessage = "El precio inicial debe ser mayor a 0")]
	public decimal StartingPrice { get; set; }

	[Range(0, double.MaxValue, ErrorMessage = "El precio de reserva debe ser mayor o igual a 0")]
	public decimal? ReservePrice { get; set; }

	[Range(0.01, double.MaxValue, ErrorMessage = "El incremento mínimo debe ser mayor a 0")]
	public decimal MinBidIncrement { get; set; } = 1.00m;

	[Required(ErrorMessage = "La fecha de inicio es requerida")]
	public DateTime StartTime { get; set; }

	[Required(ErrorMessage = "La fecha de fin es requerida")]
	public DateTime EndTime { get; set; }

	[Required(ErrorMessage = "La categoría es requerida")]
	public Guid CategoryId { get; set; }
}
