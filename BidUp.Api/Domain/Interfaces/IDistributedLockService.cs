namespace BidUp.Api.Domain.Interfaces;

/// <summary>
/// Servicio para locks distribuidos usando Redis
/// </summary>
public interface IDistributedLockService
{
	/// <summary>
	/// Adquiere un lock distribuido para una subasta
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	/// <param name="timeout">Tiempo máximo de espera para adquirir el lock</param>
	/// <param name="expiry">Tiempo de expiración del lock</param>
	/// <returns>Token del lock si se adquirió, null si no</returns>
	Task<string?> AcquireLockAsync(Guid auctionId, TimeSpan timeout, TimeSpan expiry);

	/// <summary>
	/// Libera un lock distribuido
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	/// <param name="lockToken">Token del lock a liberar</param>
	Task ReleaseLockAsync(Guid auctionId, string lockToken);
}
