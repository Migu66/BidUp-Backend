using BidUp.Api.Application.DTOs.Auction;

namespace BidUp.Api.Domain.Interfaces;

public interface IAuctionService
{
	Task<AuctionDto?> GetByIdAsync(Guid id);
	Task<(IEnumerable<AuctionDto> Auctions, int TotalCount)> GetActiveAuctionsAsync(int page = 1, int pageSize = 20);
	Task<(IEnumerable<AuctionDto> Auctions, int TotalCount)> GetAuctionsByCategoryAsync(Guid categoryId, int page = 1, int pageSize = 20);
	Task<IEnumerable<AuctionDto>> GetAuctionsBySellerAsync(Guid sellerId, int page = 1, int pageSize = 20);
	Task<AuctionDto> CreateAuctionAsync(CreateAuctionDto dto, Guid sellerId);
	Task<bool> CancelAuctionAsync(Guid auctionId, Guid sellerId);
	Task<AuctionDto> ActivateAuctionAsync(Guid auctionId, Guid sellerId);
	Task<(IEnumerable<BidDto> Bids, int TotalCount)> GetAuctionBidsAsync(Guid auctionId, int page = 1, int pageSize = 50);
}
