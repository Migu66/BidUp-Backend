using Microsoft.EntityFrameworkCore;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Enums;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Api.Application.Services;

public class AuctionService : IAuctionService
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<AuctionService> _logger;

	public AuctionService(ApplicationDbContext context, ILogger<AuctionService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task<AuctionDto?> GetByIdAsync(Guid id)
	{
		var auction = await _context.Auctions
			.Include(a => a.Seller)
			.Include(a => a.Category)
			.Include(a => a.Bids.OrderByDescending(b => b.Amount).Take(1))
				.ThenInclude(b => b.Bidder)
			.FirstOrDefaultAsync(a => a.Id == id);

		if (auction == null) return null;

		return MapToDto(auction);
	}

	public async Task<(IEnumerable<AuctionDto> Auctions, int TotalCount)> GetActiveAuctionsAsync(int page = 1, int pageSize = 20)
	{
		var query = _context.Auctions
			.Where(a => a.Status == AuctionStatus.Active && a.EndTime > DateTime.UtcNow);

		var totalCount = await query.CountAsync();

		var auctions = await query
			.Include(a => a.Seller)
			.Include(a => a.Category)
			.Include(a => a.Bids.OrderByDescending(b => b.Amount).Take(1))
				.ThenInclude(b => b.Bidder)
			.OrderBy(a => a.EndTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		// Obtener conteos de pujas para todas las subastas en una sola query
		var auctionIds = auctions.Select(a => a.Id).ToList();
		var bidCounts = await _context.Bids
			.Where(b => auctionIds.Contains(b.AuctionId))
			.GroupBy(b => b.AuctionId)
			.Select(g => new { AuctionId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.AuctionId, x => x.Count);

		return (auctions.Select(a => MapToDto(a, bidCounts.GetValueOrDefault(a.Id, 0))), totalCount);
	}

	public async Task<(IEnumerable<AuctionDto> Auctions, int TotalCount)> GetAuctionsByCategoryAsync(Guid categoryId, int page = 1, int pageSize = 20)
	{
		var query = _context.Auctions
			.Where(a => a.CategoryId == categoryId && a.Status == AuctionStatus.Active);

		var totalCount = await query.CountAsync();

		var auctions = await query
			.Include(a => a.Seller)
			.Include(a => a.Category)
			.Include(a => a.Bids.OrderByDescending(b => b.Amount).Take(1))
				.ThenInclude(b => b.Bidder)
			.OrderBy(a => a.EndTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		// Obtener conteos de pujas para todas las subastas en una sola query
		var auctionIds = auctions.Select(a => a.Id).ToList();
		var bidCounts = await _context.Bids
			.Where(b => auctionIds.Contains(b.AuctionId))
			.GroupBy(b => b.AuctionId)
			.Select(g => new { AuctionId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.AuctionId, x => x.Count);

		return (auctions.Select(a => MapToDto(a, bidCounts.GetValueOrDefault(a.Id, 0))), totalCount);
	}

	public async Task<IEnumerable<AuctionDto>> GetAuctionsBySellerAsync(Guid sellerId, int page = 1, int pageSize = 20)
	{
		var auctions = await _context.Auctions
			.Include(a => a.Seller)
			.Include(a => a.Category)
			.Include(a => a.Bids.OrderByDescending(b => b.Amount).Take(1))
				.ThenInclude(b => b.Bidder)
			.Where(a => a.SellerId == sellerId)
			.OrderByDescending(a => a.CreatedAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		// Obtener conteos de pujas para todas las subastas en una sola query
		var auctionIds = auctions.Select(a => a.Id).ToList();
		var bidCounts = await _context.Bids
			.Where(b => auctionIds.Contains(b.AuctionId))
			.GroupBy(b => b.AuctionId)
			.Select(g => new { AuctionId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.AuctionId, x => x.Count);

		return auctions.Select(a => MapToDto(a, bidCounts.GetValueOrDefault(a.Id, 0)));
	}

	public async Task<AuctionDto> CreateAuctionAsync(CreateAuctionDto dto, Guid sellerId)
	{
		// Validaciones
		if (dto.EndTime <= dto.StartTime)
		{
			throw new ArgumentException("La fecha de fin debe ser posterior a la fecha de inicio.");
		}

		if (dto.StartTime < DateTime.UtcNow.AddMinutes(-5))
		{
			throw new ArgumentException("La fecha de inicio no puede ser en el pasado.");
		}

		var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
		if (!categoryExists)
		{
			throw new ArgumentException("La categoría no existe.");
		}

		var auction = new Auction
		{
			Id = Guid.NewGuid(),
			Title = dto.Title,
			Description = dto.Description,
			ImageUrl = dto.ImageUrl,
			StartingPrice = dto.StartingPrice,
			CurrentPrice = dto.StartingPrice,
			ReservePrice = dto.ReservePrice,
			MinBidIncrement = dto.MinBidIncrement,
			StartTime = dto.StartTime,
			EndTime = dto.EndTime,
			SellerId = sellerId,
			CategoryId = dto.CategoryId,
			Status = dto.StartTime <= DateTime.UtcNow ? AuctionStatus.Active : AuctionStatus.Pending,
			CreatedAt = DateTime.UtcNow
		};

		_context.Auctions.Add(auction);
		await _context.SaveChangesAsync();

		_logger.LogInformation("Subasta creada: {AuctionId} por vendedor {SellerId}", auction.Id, sellerId);

		// Recargar con includes
		return (await GetByIdAsync(auction.Id))!;
	}

	public async Task<bool> CancelAuctionAsync(Guid auctionId, Guid sellerId)
	{
		var auction = await _context.Auctions
			.Include(a => a.Bids)
			.FirstOrDefaultAsync(a => a.Id == auctionId && a.SellerId == sellerId);

		if (auction == null) return false;

		// Solo se puede cancelar si no hay pujas
		if (auction.Bids.Any())
		{
			throw new InvalidOperationException("No se puede cancelar una subasta con pujas.");
		}

		auction.Status = AuctionStatus.Cancelled;
		auction.UpdatedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		_logger.LogInformation("Subasta cancelada: {AuctionId}", auctionId);
		return true;
	}

	public async Task<AuctionDto> ActivateAuctionAsync(Guid auctionId, Guid sellerId)
	{
		var auction = await _context.Auctions
			.Include(a => a.Seller)
			.Include(a => a.Category)
			.FirstOrDefaultAsync(a => a.Id == auctionId && a.SellerId == sellerId);

		if (auction == null)
		{
			throw new InvalidOperationException("Subasta no encontrada o no tienes permisos para activarla.");
		}

		if (auction.Status != AuctionStatus.Pending)
		{
			throw new InvalidOperationException($"La subasta no se puede activar. Estado actual: {auction.Status}");
		}

		if (auction.EndTime <= DateTime.UtcNow)
		{
			throw new InvalidOperationException("No se puede activar una subasta cuya fecha de fin ya ha pasado.");
		}

		auction.Status = AuctionStatus.Active;
		auction.StartTime = DateTime.UtcNow;
		auction.UpdatedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		_logger.LogInformation("Subasta activada: {AuctionId} por vendedor {SellerId}", auctionId, sellerId);

		return (await GetByIdAsync(auctionId))!;
	}

	public async Task<(IEnumerable<BidDto> Bids, int TotalCount)> GetAuctionBidsAsync(Guid auctionId, int page = 1, int pageSize = 50)
	{
		var query = _context.Bids
			.Where(b => b.AuctionId == auctionId);

		var totalCount = await query.CountAsync();

		var bids = await query
			.Include(b => b.Bidder)
			.OrderByDescending(b => b.Timestamp)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		var bidDtos = bids.Select(b => new BidDto
		{
			Id = b.Id,
			Amount = b.Amount,
			Timestamp = b.Timestamp,
			IsWinning = b.IsWinning,
			BidderId = b.BidderId,
			BidderName = b.Bidder.FullName,
			AuctionId = b.AuctionId
		});

		return (bidDtos, totalCount);
	}

	public async Task<int> GetActiveAuctionsCountAsync()
	{
		return await _context.Auctions
			.Where(a => a.Status == AuctionStatus.Active)
			.CountAsync();
	}

	private AuctionDto MapToDto(Auction auction, int? totalBidsCount = null)
	{
		var latestBid = auction.Bids.FirstOrDefault();

		// Si no se proporciona el conteo, obtenerlo de la BD (para llamadas individuales)
		var totalBids = totalBidsCount ?? _context.Bids.Count(b => b.AuctionId == auction.Id);

		return new AuctionDto
		{
			Id = auction.Id,
			Title = auction.Title,
			Description = auction.Description,
			ImageUrl = auction.ImageUrl,
			StartingPrice = auction.StartingPrice,
			CurrentPrice = auction.CurrentPrice,
			MinBidIncrement = auction.MinBidIncrement,
			StartTime = auction.StartTime,
			EndTime = auction.EndTime,
			Status = auction.Status.ToString(),
			TotalBids = totalBids,
			TimeRemaining = auction.TimeRemaining,
			SellerId = auction.SellerId,
			SellerName = auction.Seller.FullName,
			CategoryId = auction.CategoryId,
			CategoryName = auction.Category.Name,
			LatestBid = latestBid != null ? new BidDto
			{
				Id = latestBid.Id,
				Amount = latestBid.Amount,
				Timestamp = latestBid.Timestamp,
				IsWinning = latestBid.IsWinning,
				BidderId = latestBid.BidderId,
				BidderName = latestBid.Bidder.FullName,
				AuctionId = latestBid.AuctionId
			} : null
		};
	}
}
