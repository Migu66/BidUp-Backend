using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BidUp.Api.Application.Services;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Enums;

namespace BidUp.Tests.Services;

public class AuctionServiceTests
{
	private readonly ApplicationDbContext _context;
	private readonly Mock<ILogger<AuctionService>> _mockLogger;
	private readonly AuctionService _auctionService;

	public AuctionServiceTests()
	{
		// Configurar DbContext con InMemory
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;
		_context = new ApplicationDbContext(options);

		// Configurar Logger mock
		_mockLogger = new Mock<ILogger<AuctionService>>();

		// Crear servicio
		_auctionService = new AuctionService(_context, _mockLogger.Object);
	}

	#region GetByIdAsync Tests

	[Fact]
	public async Task GetByIdAsync_WithExistingId_ReturnsAuction()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();
		var auction = CreateTestAuction(user.Id, category.Id);

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetByIdAsync(auction.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(auction.Id);
		result.Title.Should().Be(auction.Title);
		result.SellerName.Should().Be(user.FullName);
		result.CategoryName.Should().Be(category.Name);
	}

	[Fact]
	public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
	{
		// Arrange
		var nonExistingId = Guid.NewGuid();

		// Act
		var result = await _auctionService.GetByIdAsync(nonExistingId);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task GetByIdAsync_WithBids_ReturnsLatestBid()
	{
		// Arrange
		var user = CreateTestUser();
		var bidder = CreateTestUser("bidder@test.com", "bidder");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(user.Id, category.Id);

		var bid = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 150,
			Timestamp = DateTime.UtcNow,
			IsWinning = true
		};

		_context.Users.AddRange(user, bidder);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		_context.Bids.Add(bid);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetByIdAsync(auction.Id);

		// Assert
		result.Should().NotBeNull();
		result!.LatestBid.Should().NotBeNull();
		result.LatestBid!.Amount.Should().Be(150);
		result.LatestBid.BidderName.Should().Be(bidder.FullName);
	}

	#endregion

	#region GetActiveAuctionsAsync Tests

	[Fact]
	public async Task GetActiveAuctionsAsync_ReturnsOnlyActiveAuctions()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();

		var activeAuction = CreateTestAuction(user.Id, category.Id, "Active Auction", AuctionStatus.Active);
		var pendingAuction = CreateTestAuction(user.Id, category.Id, "Pending Auction", AuctionStatus.Pending);
		var completedAuction = CreateTestAuction(user.Id, category.Id, "Completed Auction", AuctionStatus.Completed);

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(activeAuction, pendingAuction, completedAuction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetActiveAuctionsAsync();

		// Assert
		result.Auctions.Should().HaveCount(1);
		result.Auctions.First().Title.Should().Be("Active Auction");
	}

	[Fact]
	public async Task GetActiveAuctionsAsync_OrdersByEndTime()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();

		var auction1 = CreateTestAuction(user.Id, category.Id, "Auction 1", AuctionStatus.Active,
			endTime: DateTime.UtcNow.AddDays(3));
		var auction2 = CreateTestAuction(user.Id, category.Id, "Auction 2", AuctionStatus.Active,
			endTime: DateTime.UtcNow.AddDays(1));
		var auction3 = CreateTestAuction(user.Id, category.Id, "Auction 3", AuctionStatus.Active,
			endTime: DateTime.UtcNow.AddDays(2));

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(auction1, auction2, auction3);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetActiveAuctionsAsync();

		// Assert
		result.Auctions.Should().HaveCount(3);
		var auctionList = result.Auctions.ToList();
		auctionList[0].Title.Should().Be("Auction 2"); // Termina primero
		auctionList[1].Title.Should().Be("Auction 3");
		auctionList[2].Title.Should().Be("Auction 1"); // Termina último
	}

	[Fact]
	public async Task GetActiveAuctionsAsync_WithPagination_ReturnsCorrectPage()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();

		for (int i = 1; i <= 25; i++)
		{
			var auction = CreateTestAuction(user.Id, category.Id, $"Auction {i}", AuctionStatus.Active,
				endTime: DateTime.UtcNow.AddDays(i));
			_context.Auctions.Add(auction);
		}

		_context.Users.Add(user);
		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		// Act
		var page1 = await _auctionService.GetActiveAuctionsAsync(page: 1, pageSize: 10);
		var page2 = await _auctionService.GetActiveAuctionsAsync(page: 2, pageSize: 10);

		// Assert
		page1.Auctions.Should().HaveCount(10);
		page2.Auctions.Should().HaveCount(10);
	}

	[Fact]
	public async Task GetActiveAuctionsAsync_ExcludesExpiredAuctions()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();

		var activeAuction = CreateTestAuction(user.Id, category.Id, "Active", AuctionStatus.Active,
			endTime: DateTime.UtcNow.AddDays(1));
		var expiredAuction = CreateTestAuction(user.Id, category.Id, "Expired", AuctionStatus.Active,
			endTime: DateTime.UtcNow.AddDays(-1)); // Ya expiró

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(activeAuction, expiredAuction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetActiveAuctionsAsync();

		// Assert
		result.Auctions.Should().HaveCount(1);
		result.Auctions.First().Title.Should().Be("Active");
	}

	#endregion

	#region GetAuctionsByCategoryAsync Tests

	[Fact]
	public async Task GetAuctionsByCategoryAsync_ReturnsOnlyAuctionsFromCategory()
	{
		// Arrange
		var user = CreateTestUser();
		var category1 = CreateTestCategory("Electronics");
		var category2 = CreateTestCategory("Fashion");

		var auction1 = CreateTestAuction(user.Id, category1.Id, "Laptop", AuctionStatus.Active);
		var auction2 = CreateTestAuction(user.Id, category1.Id, "Phone", AuctionStatus.Active);
		var auction3 = CreateTestAuction(user.Id, category2.Id, "Shirt", AuctionStatus.Active);

		_context.Users.Add(user);
		_context.Categories.AddRange(category1, category2);
		_context.Auctions.AddRange(auction1, auction2, auction3);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionsByCategoryAsync(category1.Id);

		// Assert
		result.Auctions.Should().HaveCount(2);
		result.Auctions.Should().Contain(a => a.Title == "Laptop");
		result.Auctions.Should().Contain(a => a.Title == "Phone");
		result.Auctions.Should().NotContain(a => a.Title == "Shirt");
	}

	[Fact]
	public async Task GetAuctionsByCategoryAsync_ReturnsOnlyActiveAuctions()
	{
		// Arrange
		var user = CreateTestUser();
		var category = CreateTestCategory();

		var activeAuction = CreateTestAuction(user.Id, category.Id, "Active", AuctionStatus.Active);
		var pendingAuction = CreateTestAuction(user.Id, category.Id, "Pending", AuctionStatus.Pending);

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(activeAuction, pendingAuction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionsByCategoryAsync(category.Id);

		// Assert
		result.Auctions.Should().HaveCount(1);
		result.Auctions.First().Title.Should().Be("Active");
	}

	#endregion

	#region GetAuctionsBySellerAsync Tests

	[Fact]
	public async Task GetAuctionsBySellerAsync_ReturnsOnlySellerAuctions()
	{
		// Arrange
		var seller1 = CreateTestUser("seller1@test.com", "seller1");
		var seller2 = CreateTestUser("seller2@test.com", "seller2");
		var category = CreateTestCategory();

		var auction1 = CreateTestAuction(seller1.Id, category.Id, "Seller1 Auction 1");
		var auction2 = CreateTestAuction(seller1.Id, category.Id, "Seller1 Auction 2");
		var auction3 = CreateTestAuction(seller2.Id, category.Id, "Seller2 Auction");

		_context.Users.AddRange(seller1, seller2);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(auction1, auction2, auction3);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionsBySellerAsync(seller1.Id);

		// Assert
		result.Should().HaveCount(2);
		result.Should().Contain(a => a.Title == "Seller1 Auction 1");
		result.Should().Contain(a => a.Title == "Seller1 Auction 2");
		result.Should().NotContain(a => a.Title == "Seller2 Auction");
	}

	[Fact]
	public async Task GetAuctionsBySellerAsync_OrdersByCreatedAtDescending()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();

		var auction1 = CreateTestAuction(seller.Id, category.Id, "Oldest");
		auction1.CreatedAt = DateTime.UtcNow.AddDays(-3);

		var auction2 = CreateTestAuction(seller.Id, category.Id, "Newest");
		auction2.CreatedAt = DateTime.UtcNow;

		var auction3 = CreateTestAuction(seller.Id, category.Id, "Middle");
		auction3.CreatedAt = DateTime.UtcNow.AddDays(-1);

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(auction1, auction2, auction3);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionsBySellerAsync(seller.Id);

		// Assert
		result.Should().HaveCount(3);
		var auctionList = result.ToList();
		auctionList[0].Title.Should().Be("Newest");
		auctionList[1].Title.Should().Be("Middle");
		auctionList[2].Title.Should().Be("Oldest");
	}

	#endregion

	#region CreateAuctionAsync Tests

	[Fact]
	public async Task CreateAuctionAsync_WithValidData_CreatesAuction()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		var createDto = new CreateAuctionDto
		{
			Title = "New Auction",
			Description = "This is a test auction for creating new auctions",
			StartingPrice = 100,
			MinBidIncrement = 5,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = category.Id
		};

		// Act
		var result = await _auctionService.CreateAuctionAsync(createDto, seller.Id);

		// Assert
		result.Should().NotBeNull();
		result.Title.Should().Be("New Auction");
		result.StartingPrice.Should().Be(100);
		result.CurrentPrice.Should().Be(100);
		result.Status.Should().Be("Active");

		var auctionInDb = await _context.Auctions.FindAsync(result.Id);
		auctionInDb.Should().NotBeNull();
	}

	[Fact]
	public async Task CreateAuctionAsync_WithEndTimeBeforeStartTime_ThrowsArgumentException()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		var createDto = new CreateAuctionDto
		{
			Title = "Invalid Auction",
			Description = "This auction has invalid dates",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow.AddDays(7),
			EndTime = DateTime.UtcNow.AddDays(1), // Antes del inicio
			CategoryId = category.Id
		};

		// Act
		Func<Task> act = async () => await _auctionService.CreateAuctionAsync(createDto, seller.Id);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("La fecha de fin debe ser posterior a la fecha de inicio.");
	}

	[Fact]
	public async Task CreateAuctionAsync_WithStartTimeInPast_ThrowsArgumentException()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		var createDto = new CreateAuctionDto
		{
			Title = "Past Auction",
			Description = "This auction starts in the past",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow.AddDays(-1),
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = category.Id
		};

		// Act
		Func<Task> act = async () => await _auctionService.CreateAuctionAsync(createDto, seller.Id);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("La fecha de inicio no puede ser en el pasado.");
	}

	[Fact]
	public async Task CreateAuctionAsync_WithNonExistentCategory_ThrowsArgumentException()
	{
		// Arrange
		var seller = CreateTestUser();
		_context.Users.Add(seller);
		await _context.SaveChangesAsync();

		var createDto = new CreateAuctionDto
		{
			Title = "No Category Auction",
			Description = "This auction has invalid category",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = Guid.NewGuid() // No existe
		};

		// Act
		Func<Task> act = async () => await _auctionService.CreateAuctionAsync(createDto, seller.Id);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("La categoría no existe.");
	}

	[Fact]
	public async Task CreateAuctionAsync_WithFutureStartTime_CreatesPendingAuction()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		var createDto = new CreateAuctionDto
		{
			Title = "Future Auction",
			Description = "This auction starts in the future",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow.AddHours(2),
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = category.Id
		};

		// Act
		var result = await _auctionService.CreateAuctionAsync(createDto, seller.Id);

		// Assert
		result.Status.Should().Be("Pending");
	}

	#endregion

	#region CancelAuctionAsync Tests

	[Fact]
	public async Task CancelAuctionAsync_WithValidAuctionAndNoBids_CancelsAuction()
	{
		// Arrange
		var seller = CreateTestUser();
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		_context.Users.Add(seller);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.CancelAuctionAsync(auction.Id, seller.Id);

		// Assert
		result.Should().BeTrue();

		var cancelledAuction = await _context.Auctions.FindAsync(auction.Id);
		cancelledAuction!.Status.Should().Be(AuctionStatus.Cancelled);
	}

	[Fact]
	public async Task CancelAuctionAsync_WithNonExistentAuction_ReturnsFalse()
	{
		// Arrange
		var seller = CreateTestUser();
		_context.Users.Add(seller);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.CancelAuctionAsync(Guid.NewGuid(), seller.Id);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CancelAuctionAsync_WithWrongSeller_ReturnsFalse()
	{
		// Arrange
		var seller = CreateTestUser();
		var otherSeller = CreateTestUser("other@test.com", "other");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		_context.Users.AddRange(seller, otherSeller);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.CancelAuctionAsync(auction.Id, otherSeller.Id);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CancelAuctionAsync_WithBids_ThrowsInvalidOperationException()
	{
		// Arrange
		var seller = CreateTestUser();
		var bidder = CreateTestUser("bidder@test.com", "bidder");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		var bid = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 150,
			Timestamp = DateTime.UtcNow,
			IsWinning = true
		};

		_context.Users.AddRange(seller, bidder);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		_context.Bids.Add(bid);
		await _context.SaveChangesAsync();

		// Act
		Func<Task> act = async () => await _auctionService.CancelAuctionAsync(auction.Id, seller.Id);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("No se puede cancelar una subasta con pujas.");
	}

	#endregion

	#region GetAuctionBidsAsync Tests

	[Fact]
	public async Task GetAuctionBidsAsync_ReturnsAllBidsForAuction()
	{
		// Arrange
		var seller = CreateTestUser();
		var bidder1 = CreateTestUser("bidder1@test.com", "bidder1");
		var bidder2 = CreateTestUser("bidder2@test.com", "bidder2");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		var bid1 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder1.Id,
			Amount = 110,
			Timestamp = DateTime.UtcNow.AddMinutes(-10),
			IsWinning = false
		};

		var bid2 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder2.Id,
			Amount = 120,
			Timestamp = DateTime.UtcNow.AddMinutes(-5),
			IsWinning = true
		};

		_context.Users.AddRange(seller, bidder1, bidder2);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		_context.Bids.AddRange(bid1, bid2);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionBidsAsync(auction.Id);

		// Assert
		result.Bids.Should().HaveCount(2);
		var bidList = result.Bids.ToList();
		bidList[0].Amount.Should().Be(120); // Más reciente primero
		bidList[1].Amount.Should().Be(110);
	}

	[Fact]
	public async Task GetAuctionBidsAsync_OrdersByTimestampDescending()
	{
		// Arrange
		var seller = CreateTestUser();
		var bidder = CreateTestUser("bidder@test.com", "bidder");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		for (int i = 1; i <= 5; i++)
		{
			var bid = new Bid
			{
				Id = Guid.NewGuid(),
				AuctionId = auction.Id,
				BidderId = bidder.Id,
				Amount = 100 + (i * 10),
				Timestamp = DateTime.UtcNow.AddMinutes(-i),
				IsWinning = i == 5
			};
			_context.Bids.Add(bid);
		}

		_context.Users.AddRange(seller, bidder);
		_context.Categories.Add(category);
		_context.Auctions.Add(auction);
		await _context.SaveChangesAsync();

		// Act
		var result = await _auctionService.GetAuctionBidsAsync(auction.Id);

		// Assert
		result.Bids.Should().HaveCount(5);
		var bidList = result.Bids.ToList();
		bidList[0].Amount.Should().Be(110); // Más reciente
		bidList[4].Amount.Should().Be(150); // Más antiguo
	}

	#endregion

	#region Helper Methods

	private User CreateTestUser(string email = "test@example.com", string username = "testuser")
	{
		return new User
		{
			Id = Guid.NewGuid(),
			UserName = username,
			Email = email,
			FirstName = "Test",
			LastName = "User",
			IsActive = true
		};
	}

	private Category CreateTestCategory(string name = "Test Category")
	{
		return new Category
		{
			Id = Guid.NewGuid(),
			Name = name,
			Description = "Test category description",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
	}

	private Auction CreateTestAuction(Guid sellerId, Guid categoryId, string title = "Test Auction",
		AuctionStatus status = AuctionStatus.Active, DateTime? endTime = null)
	{
		return new Auction
		{
			Id = Guid.NewGuid(),
			Title = title,
			Description = "Test auction description for testing purposes",
			StartingPrice = 100,
			CurrentPrice = 100,
			MinBidIncrement = 5,
			StartTime = DateTime.UtcNow,
			EndTime = endTime ?? DateTime.UtcNow.AddDays(7),
			Status = status,
			SellerId = sellerId,
			CategoryId = categoryId,
			CreatedAt = DateTime.UtcNow
		};
	}

	#endregion
}
