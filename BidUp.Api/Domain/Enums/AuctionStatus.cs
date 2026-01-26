namespace BidUp.Api.Domain.Enums;

public enum AuctionStatus
{
	/// <summary>
	/// Subasta creada pero aún no activa
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Subasta activa y aceptando pujas
	/// </summary>
	Active = 1,

	/// <summary>
	/// Subasta finalizada con ganador
	/// </summary>
	Completed = 2,

	/// <summary>
	/// Subasta cancelada por el vendedor o admin
	/// </summary>
	Cancelled = 3,

	/// <summary>
	/// Subasta expirada sin pujas
	/// </summary>
	Expired = 4
}
