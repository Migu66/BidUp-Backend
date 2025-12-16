using BidUp.Api.Application.DTOs.Auction;

namespace BidUp.Api.Domain.Interfaces;

public interface IAuctionService
{
	Task<AuctionDto?> GetByIdAsync(Guid id);
	Task<IEnumerable<AuctionDto>> GetActiveAuctionsAsync(int page = 1, int pageSize = 20);
	Task<IEnumerable<AuctionDto>> GetAuctionsByCategoryAsync(Guid categoryId, int page = 1, int pageSize = 20);
	Task<IEnumerable<AuctionDto>> GetAuctionsBySellerAsync(Guid sellerId, int page = 1, int pageSize = 20);
	Task<AuctionDto> CreateAuctionAsync(CreateAuctionDto dto, Guid sellerId);
	Task<bool> CancelAuctionAsync(Guid auctionId, Guid sellerId);
	Task<IEnumerable<BidDto>> GetAuctionBidsAsync(Guid auctionId, int page = 1, int pageSize = 50);
}
