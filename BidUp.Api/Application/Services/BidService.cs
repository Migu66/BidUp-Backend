using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Enums;
using BidUp.Api.Domain.Interfaces;
using BidUp.Api.Hubs;

namespace BidUp.Api.Application.Services;

public class BidService : IBidService
{
	private readonly ApplicationDbContext _context;
	private readonly IDistributedLockService _lockService;
	private readonly IHubContext<AuctionHub> _hubContext;
	private readonly ILogger<BidService> _logger;

	// Configuración de locks
	private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);

	public BidService(
		ApplicationDbContext context,
		IDistributedLockService lockService,
		IHubContext<AuctionHub> hubContext,
		ILogger<BidService> logger)
	{
		_context = context;
		_lockService = lockService;
		_hubContext = hubContext;
		_logger = logger;
	}

	public async Task<BidResult> PlaceBidAsync(Guid auctionId, Guid bidderId, decimal amount, string? ipAddress)
	{
		// 1. Adquirir lock distribuido para esta subasta
		var lockToken = await _lockService.AcquireLockAsync(auctionId, LockTimeout, LockExpiry);

		if (lockToken == null)
		{
			_logger.LogWarning("No se pudo adquirir lock para puja en subasta {AuctionId}", auctionId);
			return BidResult.Failed("El servidor está procesando muchas pujas. Intenta de nuevo.");
		}

		try
		{
			// 2. Obtener la subasta con la puja más alta actual
			var auction = await _context.Auctions
				.Include(a => a.Bids.OrderByDescending(b => b.Amount).Take(1))
				.FirstOrDefaultAsync(a => a.Id == auctionId);

			if (auction == null)
			{
				return BidResult.Failed("La subasta no existe.");
			}

			// 3. Validaciones de negocio
			var validationResult = ValidateBid(auction, bidderId, amount);
			if (!validationResult.IsValid)
			{
				return BidResult.Failed(validationResult.Error!);
			}

			// 4. Obtener el postor anterior (para notificarle que fue superado)
			var previousHighBid = auction.Bids.FirstOrDefault();
			var previousHighBidderId = previousHighBid?.BidderId;

			// 5. Crear la nueva puja con timestamp preciso
			var bid = new Bid
			{
				Id = Guid.NewGuid(),
				AuctionId = auctionId,
				BidderId = bidderId,
				Amount = amount,
				Timestamp = DateTime.UtcNow, // Timestamp del servidor para consistencia
				IsWinning = true,
				IpAddress = ipAddress
			};

			// 6. Marcar la puja anterior como no ganadora
			if (previousHighBid != null)
			{
				previousHighBid.IsWinning = false;
			}

			// 7. Actualizar el precio actual de la subasta
			auction.CurrentPrice = amount;
			auction.UpdatedAt = DateTime.UtcNow;

			// 8. Guardar en la base de datos
			_context.Bids.Add(bid);
			await _context.SaveChangesAsync();

			_logger.LogInformation(
				"Puja exitosa: Subasta {AuctionId}, Usuario {BidderId}, Monto {Amount}",
				auctionId, bidderId, amount);

			// 9. Crear DTO para la respuesta
			var bidder = await _context.Users.FindAsync(bidderId);
			var bidDto = new BidDto
			{
				Id = bid.Id,
				Amount = bid.Amount,
				Timestamp = bid.Timestamp,
				IsWinning = bid.IsWinning,
				BidderId = bid.BidderId,
				BidderName = bidder?.FullName ?? "Usuario",
				AuctionId = bid.AuctionId
			};

			// 10. Notificar a todos los participantes via SignalR
			await NotifyNewBidAsync(auction, bidDto, previousHighBidderId);

			return BidResult.Succeeded(bidDto, amount, previousHighBidderId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error al procesar puja en subasta {AuctionId}", auctionId);
			return BidResult.Failed("Error interno al procesar la puja.");
		}
		finally
		{
			// 11. Siempre liberar el lock
			await _lockService.ReleaseLockAsync(auctionId, lockToken);
		}
	}

	public async Task<BidDto?> GetHighestBidAsync(Guid auctionId)
	{
		var bid = await _context.Bids
			.Include(b => b.Bidder)
			.Where(b => b.AuctionId == auctionId)
			.OrderByDescending(b => b.Amount)
			.FirstOrDefaultAsync();

		if (bid == null) return null;

		return new BidDto
		{
			Id = bid.Id,
			Amount = bid.Amount,
			Timestamp = bid.Timestamp,
			IsWinning = bid.IsWinning,
			BidderId = bid.BidderId,
			BidderName = bid.Bidder.FullName,
			AuctionId = bid.AuctionId
		};
	}

	public async Task<IEnumerable<BidDto>> GetUserBidsAsync(Guid userId, int page = 1, int pageSize = 20)
	{
		var bids = await _context.Bids
			.Include(b => b.Bidder)
			.Include(b => b.Auction)
			.Where(b => b.BidderId == userId)
			.OrderByDescending(b => b.Timestamp)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return bids.Select(b => new BidDto
		{
			Id = b.Id,
			Amount = b.Amount,
			Timestamp = b.Timestamp,
			IsWinning = b.IsWinning,
			BidderId = b.BidderId,
			BidderName = b.Bidder.FullName,
			AuctionId = b.AuctionId
		});
	}

	private (bool IsValid, string? Error) ValidateBid(Auction auction, Guid bidderId, decimal amount)
	{
		// Verificar que la subasta está activa
		if (auction.Status != AuctionStatus.Active)
		{
			return (false, "La subasta no está activa.");
		}

		// Verificar que no ha terminado
		if (auction.HasEnded)
		{
			return (false, "La subasta ha terminado.");
		}

		// Verificar que el vendedor no puede pujar en su propia subasta
		if (auction.SellerId == bidderId)
		{
			return (false, "No puedes pujar en tu propia subasta.");
		}

		// Calcular el monto mínimo requerido
		var minimumBid = auction.CurrentPrice + auction.MinBidIncrement;

		// Si es la primera puja, el mínimo es el precio inicial
		if (auction.Bids.Count == 0)
		{
			minimumBid = auction.StartingPrice;
		}

		// Verificar que la puja es suficiente
		if (amount < minimumBid)
		{
			return (false, $"La puja mínima es {minimumBid:C}. Tu puja: {amount:C}");
		}

		return (true, null);
	}

	private async Task NotifyNewBidAsync(Auction auction, BidDto bidDto, Guid? previousHighBidderId)
	{
		// Obtener el conteo real de pujas desde la BD
		var totalBids = await _context.Bids.CountAsync(b => b.AuctionId == auction.Id);

		// Notificar a todos los participantes de la subasta
		var notification = new BidNotificationDto
		{
			AuctionId = auction.Id,
			Bid = bidDto,
			NewCurrentPrice = auction.CurrentPrice,
			TotalBids = totalBids,
			TimeRemaining = auction.TimeRemaining
		};

		await _hubContext.NotifyNewBid(auction.Id, notification);

		// Notificar al postor anterior que fue superado
		if (previousHighBidderId.HasValue && previousHighBidderId != bidDto.BidderId)
		{
			var outbidNotification = new OutbidNotificationDto
			{
				AuctionId = auction.Id,
				AuctionTitle = auction.Title,
				YourBid = auction.CurrentPrice - auction.MinBidIncrement,
				NewHighestBid = auction.CurrentPrice,
				MinimumNextBid = auction.CurrentPrice + auction.MinBidIncrement
			};

			await _hubContext.NotifyOutbid(previousHighBidderId.Value.ToString(), outbidNotification);
		}
	}
}
