using StackExchange.Redis;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Api.Application.Services;

/// <summary>
/// Implementación de locks distribuidos usando Redis SETNX
/// para manejar pujas concurrentes de forma atómica
/// </summary>
public class RedisDistributedLockService : IDistributedLockService
{
	private readonly IConnectionMultiplexer _redis;
	private readonly ILogger<RedisDistributedLockService> _logger;
	private const string LockKeyPrefix = "auction_lock:";

	public RedisDistributedLockService(
		IConnectionMultiplexer redis,
		ILogger<RedisDistributedLockService> logger)
	{
		_redis = redis;
		_logger = logger;
	}

	public async Task<string?> AcquireLockAsync(Guid auctionId, TimeSpan timeout, TimeSpan expiry)
	{
		var db = _redis.GetDatabase();
		var lockKey = $"{LockKeyPrefix}{auctionId}";
		var lockToken = Guid.NewGuid().ToString();
		var startTime = DateTime.UtcNow;

		while (DateTime.UtcNow - startTime < timeout)
		{
			// SETNX: Solo setea si la key no existe (atómico)
			var acquired = await db.StringSetAsync(
				lockKey,
				lockToken,
				expiry,
				When.NotExists);

			if (acquired)
			{
				_logger.LogDebug("Lock adquirido para subasta {AuctionId}, token: {Token}",
					auctionId, lockToken);
				return lockToken;
			}

			// Esperar antes de reintentar (backoff pequeño)
			await Task.Delay(10);
		}

		_logger.LogWarning("No se pudo adquirir lock para subasta {AuctionId} después de {Timeout}ms",
			auctionId, timeout.TotalMilliseconds);
		return null;
	}

	public async Task ReleaseLockAsync(Guid auctionId, string lockToken)
	{
		var db = _redis.GetDatabase();
		var lockKey = $"{LockKeyPrefix}{auctionId}";

		// Lua script para liberar el lock solo si el token coincide (atómico)
		const string luaScript = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

		var result = await db.ScriptEvaluateAsync(
			luaScript,
			new RedisKey[] { lockKey },
			new RedisValue[] { lockToken });

		if ((int)result == 1)
		{
			_logger.LogDebug("Lock liberado para subasta {AuctionId}", auctionId);
		}
		else
		{
			_logger.LogWarning("Lock para subasta {AuctionId} ya expiró o fue liberado por otro proceso",
				auctionId);
		}
	}
}

/// <summary>
/// Implementación fallback cuando Redis no está disponible
/// Usa locks en memoria (solo funciona en un solo servidor)
/// </summary>
public class InMemoryDistributedLockService : IDistributedLockService
{
	private readonly ILogger<InMemoryDistributedLockService> _logger;
	private static readonly Dictionary<Guid, (string Token, DateTime Expiry)> _locks = new();
	private static readonly object _lockObject = new();

	public InMemoryDistributedLockService(ILogger<InMemoryDistributedLockService> logger)
	{
		_logger = logger;
	}

	public Task<string?> AcquireLockAsync(Guid auctionId, TimeSpan timeout, TimeSpan expiry)
	{
		var lockToken = Guid.NewGuid().ToString();
		var startTime = DateTime.UtcNow;

		while (DateTime.UtcNow - startTime < timeout)
		{
			lock (_lockObject)
			{
				// Limpiar locks expirados
				if (_locks.TryGetValue(auctionId, out var existingLock))
				{
					if (existingLock.Expiry < DateTime.UtcNow)
					{
						_locks.Remove(auctionId);
					}
					else
					{
						Thread.Sleep(10);
						continue;
					}
				}

				_locks[auctionId] = (lockToken, DateTime.UtcNow.Add(expiry));
				_logger.LogDebug("Lock en memoria adquirido para subasta {AuctionId}", auctionId);
				return Task.FromResult<string?>(lockToken);
			}
		}

		_logger.LogWarning("No se pudo adquirir lock en memoria para subasta {AuctionId}", auctionId);
		return Task.FromResult<string?>(null);
	}

	public Task ReleaseLockAsync(Guid auctionId, string lockToken)
	{
		lock (_lockObject)
		{
			if (_locks.TryGetValue(auctionId, out var existingLock) && existingLock.Token == lockToken)
			{
				_locks.Remove(auctionId);
				_logger.LogDebug("Lock en memoria liberado para subasta {AuctionId}", auctionId);
			}
		}

		return Task.CompletedTask;
	}
}
