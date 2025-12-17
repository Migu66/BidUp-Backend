using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using BidUp.Api.Application.Services;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Enums;
using BidUp.Api.Domain.Interfaces;
using BidUp.Api.Hubs;

namespace BidUp.Tests.Services;

public class BidServiceTests
{
	private readonly Mock<IDistributedLockService> _mockLockService;
	private readonly Mock<IHubContext<AuctionHub>> _mockHubContext;
	private readonly Mock<ILogger<BidService>> _mockLogger;

	public BidServiceTests()
	{
		_mockLockService = new Mock<IDistributedLockService>();
		_mockHubContext = new Mock<IHubContext<AuctionHub>>();
		_mockLogger = new Mock<ILogger<BidService>>();
		
		// Setup básico para el HubContext (evitar excepciones al enviar notificaciones)
		var mockClients = new Mock<IHubClients>();
		var mockClientProxy = new Mock<IClientProxy>();
		_mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
		mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
	}

	#region PlaceBidAsync Tests

	[Fact]
	public async Task PlaceBidAsync_WithValidBid_CreatesNewBid()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id, startingPrice: 100, minIncrement: 10);
		auction.Status = AuctionStatus.Active;

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		// Mock lock adquirido
		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 100m, "192.168.1.1");

		// Assert
		result.Success.Should().BeTrue(result.ErrorMessage ?? "no error message");
		result.Bid.Should().NotBeNull();
		result.Bid!.Amount.Should().Be(100m);
		result.Bid.BidderId.Should().Be(bidder.Id);
		result.Bid.IsWinning.Should().BeTrue();
		result.NewCurrentPrice.Should().Be(100m);

		var savedBid = await context.Bids.FirstOrDefaultAsync();
		savedBid.Should().NotBeNull();
		savedBid!.Amount.Should().Be(100m);
		savedBid.BidderId.Should().Be(bidder.Id);
		savedBid.IsWinning.Should().BeTrue();
	}

	[Fact]
	public async Task PlaceBidAsync_WithoutLock_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var auction = CreateTestAuction(Guid.NewGuid(), Guid.NewGuid());

		// Mock lock NO adquirido
		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync((string?)null);

		// Act
		var result = await service.PlaceBidAsync(auction.Id, Guid.NewGuid(), 100m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("procesando muchas pujas");
	}

	[Fact]
	public async Task PlaceBidAsync_WithNonExistentAuction_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var auctionId = Guid.NewGuid();

		_mockLockService.Setup(x => x.AcquireLockAsync(auctionId, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auctionId, Guid.NewGuid(), 100m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Be("La subasta no existe.");

		// Verificar que se liberó el lock
		_mockLockService.Verify(x => x.ReleaseLockAsync(auctionId, "lock-token"), Times.Once);
	}

	[Fact]
	public async Task PlaceBidAsync_WhenAuctionNotActive_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);
		auction.Status = AuctionStatus.Pending;

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 100m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Be("La subasta no está activa.");
	}

	[Fact]
	public async Task PlaceBidAsync_WhenAuctionEnded_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);
		auction.Status = AuctionStatus.Active;
		auction.EndTime = DateTime.UtcNow.AddHours(-1); // Ya terminó

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 100m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Be("La subasta ha terminado.");
	}

	[Fact]
	public async Task PlaceBidAsync_WhenSellerBidsOnOwnAuction_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);
		auction.Status = AuctionStatus.Active;

		context.Users.Add(seller);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, seller.Id, 100m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Be("No puedes pujar en tu propia subasta.");
	}

	[Fact]
	public async Task PlaceBidAsync_WithBelowMinimumAmount_ReturnsFailure()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id, startingPrice: 100, minIncrement: 10);
		auction.Status = AuctionStatus.Active;

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act - Puja de 50 cuando el mínimo es 100
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 50m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("La puja mínima es");
	}

	[Fact]
	public async Task PlaceBidAsync_SecondBid_RequiresMinimumIncrement()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder1 = CreateTestUser("bidder1@test.com");
		var bidder2 = CreateTestUser("bidder2@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id, startingPrice: 100, minIncrement: 10);
		auction.Status = AuctionStatus.Active;
		auction.CurrentPrice = 100; // Ya hay una puja de 100

		// Primera puja existente
		var firstBid = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder1.Id,
			Amount = 100,
			IsWinning = true,
			Timestamp = DateTime.UtcNow.AddMinutes(-5)
		};

		context.Users.AddRange(seller, bidder1, bidder2);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.Add(firstBid);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act - Intentar pujar 105 cuando el mínimo es 110 (100 + 10)
		var result = await service.PlaceBidAsync(auction.Id, bidder2.Id, 105m, null);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("La puja mínima es");
	}

	[Fact]
	public async Task PlaceBidAsync_SecondBidValid_MarksPreviousAsNotWinning()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder1 = CreateTestUser("bidder1@test.com");
		var bidder2 = CreateTestUser("bidder2@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id, startingPrice: 100, minIncrement: 10);
		auction.Status = AuctionStatus.Active;
		auction.CurrentPrice = 100;

		// Primera puja existente
		var firstBid = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder1.Id,
			Amount = 100,
			IsWinning = true,
			Timestamp = DateTime.UtcNow.AddMinutes(-5)
		};

		context.Users.AddRange(seller, bidder1, bidder2);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.Add(firstBid);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act - Puja válida de 110
		var result = await service.PlaceBidAsync(auction.Id, bidder2.Id, 110m, null);

		// Assert
		result.Success.Should().BeTrue();
		result.PreviousHighBidderId.Should().Be(bidder1.Id);

		var previousBid = await context.Bids.FindAsync(firstBid.Id);
		previousBid!.IsWinning.Should().BeFalse();

		var newBid = await context.Bids
			.Where(b => b.BidderId == bidder2.Id)
			.FirstOrDefaultAsync();
		newBid.Should().NotBeNull();
		newBid!.IsWinning.Should().BeTrue();
		newBid.Amount.Should().Be(110m);
	}

	[Fact]
	public async Task PlaceBidAsync_UpdatesAuctionCurrentPrice()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id, startingPrice: 100, minIncrement: 10);
		auction.Status = AuctionStatus.Active;

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 150m, null);

		// Assert
		result.Success.Should().BeTrue();

		var updatedAuction = await context.Auctions.FindAsync(auction.Id);
		updatedAuction!.CurrentPrice.Should().Be(150m);
		updatedAuction.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task PlaceBidAsync_WithIpAddress_SavesIpForAudit()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);
		auction.Status = AuctionStatus.Active;

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		_mockLockService.Setup(x => x.AcquireLockAsync(auction.Id, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act
		var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 100m, "192.168.1.100");

		// Assert
		result.Success.Should().BeTrue();

		var savedBid = await context.Bids.FirstOrDefaultAsync();
		savedBid!.IpAddress.Should().Be("192.168.1.100");
	}

	[Fact]
	public async Task PlaceBidAsync_AlwaysReleasesLock()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var auctionId = Guid.NewGuid();

		_mockLockService.Setup(x => x.AcquireLockAsync(auctionId, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
			.ReturnsAsync("lock-token");

		// Act - Llamada que fallará porque no existe la subasta
		var result = await service.PlaceBidAsync(auctionId, Guid.NewGuid(), 100m, null);

		// Assert
		result.Success.Should().BeFalse();

		// Verificar que el lock se liberó incluso con error
		_mockLockService.Verify(x => x.ReleaseLockAsync(auctionId, "lock-token"), Times.Once);
	}

	#endregion

	#region GetHighestBidAsync Tests

	[Fact]
	public async Task GetHighestBidAsync_WithBids_ReturnsHighestBid()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder1 = CreateTestUser("bidder1@test.com");
		var bidder2 = CreateTestUser("bidder2@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		var bid1 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder1.Id,
			Amount = 100,
			Timestamp = DateTime.UtcNow.AddMinutes(-10)
		};

		var bid2 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder2.Id,
			Amount = 150, // Puja más alta
			Timestamp = DateTime.UtcNow.AddMinutes(-5)
		};

		context.Users.AddRange(seller, bidder1, bidder2);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.AddRange(bid1, bid2);
		await context.SaveChangesAsync();

		// Act
		var result = await service.GetHighestBidAsync(auction.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Amount.Should().Be(150);
		result.BidderId.Should().Be(bidder2.Id);
	}

	[Fact]
	public async Task GetHighestBidAsync_WithNoBids_ReturnsNull()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		context.Users.Add(seller);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		await context.SaveChangesAsync();

		// Act
		var result = await service.GetHighestBidAsync(auction.Id);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task GetHighestBidAsync_IncludesBidderName()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com", "John", "Doe");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		var bid = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 100
		};

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.Add(bid);
		await context.SaveChangesAsync();

		// Act
		var result = await service.GetHighestBidAsync(auction.Id);

		// Assert
		result.Should().NotBeNull();
		result!.BidderName.Should().Be("John Doe");
	}

	#endregion

	#region GetUserBidsAsync Tests

	[Fact]
	public async Task GetUserBidsAsync_ReturnsUserBids()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com", "Jane", "Doe");
		var category = CreateTestCategory();
		var auction1 = CreateTestAuction(seller.Id, category.Id);
		var auction2 = CreateTestAuction(seller.Id, category.Id);

		var bid1 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction1.Id,
			BidderId = bidder.Id,
			Amount = 100,
			Timestamp = DateTime.UtcNow.AddMinutes(-10)
		};

		var bid2 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction2.Id,
			BidderId = bidder.Id,
			Amount = 200,
			Timestamp = DateTime.UtcNow.AddMinutes(-5)
		};

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.AddRange(auction1, auction2);
		context.Bids.AddRange(bid1, bid2);
		await context.SaveChangesAsync();

		// Act
		var result = await service.GetUserBidsAsync(bidder.Id);

		// Assert
		result.Should().HaveCount(2);
		result.Should().AllSatisfy(b => b.BidderId.Should().Be(bidder.Id));
		result.Should().AllSatisfy(b => b.BidderName.Should().Be("Jane Doe"));
	}

	[Fact]
	public async Task GetUserBidsAsync_OrdersByTimestampDescending()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		var bid1 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 100,
			Timestamp = DateTime.UtcNow.AddMinutes(-20)
		};

		var bid2 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 150,
			Timestamp = DateTime.UtcNow.AddMinutes(-10)
		};

		var bid3 = new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 200,
			Timestamp = DateTime.UtcNow.AddMinutes(-5)
		};

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.AddRange(bid1, bid2, bid3);
		await context.SaveChangesAsync();

		// Act
		var result = (await service.GetUserBidsAsync(bidder.Id)).ToList();

		// Assert
		result.Should().HaveCount(3);
		result[0].Amount.Should().Be(200); // Más reciente
		result[1].Amount.Should().Be(150);
		result[2].Amount.Should().Be(100); // Más antigua
	}

	[Fact]
	public async Task GetUserBidsAsync_WithPagination_ReturnsCorrectPage()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var seller = CreateTestUser("seller@test.com");
		var bidder = CreateTestUser("bidder@test.com");
		var category = CreateTestCategory();
		var auction = CreateTestAuction(seller.Id, category.Id);

		// Crear 15 pujas
		var bids = Enumerable.Range(1, 15).Select(i => new Bid
		{
			Id = Guid.NewGuid(),
			AuctionId = auction.Id,
			BidderId = bidder.Id,
			Amount = 100 + i,
			Timestamp = DateTime.UtcNow.AddMinutes(-i)
		}).ToList();

		context.Users.AddRange(seller, bidder);
		context.Categories.Add(category);
		context.Auctions.Add(auction);
		context.Bids.AddRange(bids);
		await context.SaveChangesAsync();

		// Act - Obtener página 2 con 10 elementos por página
		var result = await service.GetUserBidsAsync(bidder.Id, page: 2, pageSize: 10);

		// Assert
		result.Should().HaveCount(5); // Quedan 5 elementos en la página 2
	}

	[Fact]
	public async Task GetUserBidsAsync_WithNoBids_ReturnsEmptyList()
	{
		// Arrange
		var context = CreateInMemoryContext();
		var service = new BidService(context, _mockLockService.Object, _mockHubContext.Object, _mockLogger.Object);

		var bidder = CreateTestUser("bidder@test.com");
		context.Users.Add(bidder);
		await context.SaveChangesAsync();

		// Act
		var result = await service.GetUserBidsAsync(bidder.Id);

		// Assert
		result.Should().BeEmpty();
	}

	#endregion

	#region Helper Methods

	private ApplicationDbContext CreateInMemoryContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		return new ApplicationDbContext(options);
	}

	private User CreateTestUser(string email, string firstName = "Test", string lastName = "User")
	{
		return new User
		{
			Id = Guid.NewGuid(),
			Email = email,
			UserName = email,
			FirstName = firstName,
			LastName = lastName,
			CreatedAt = DateTime.UtcNow
		};
	}

	private Category CreateTestCategory(string name = "Test Category")
	{
		return new Category
		{
			Id = Guid.NewGuid(),
			Name = name,
			Description = "Test category description",
			CreatedAt = DateTime.UtcNow
		};
	}

	private Auction CreateTestAuction(
		Guid sellerId,
		Guid categoryId,
		decimal startingPrice = 100,
		decimal minIncrement = 5)
	{
		return new Auction
		{
			Id = Guid.NewGuid(),
			Title = "Test Auction",
			Description = "Test auction description",
			StartingPrice = startingPrice,
			CurrentPrice = startingPrice,
			MinBidIncrement = minIncrement,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			Status = AuctionStatus.Pending,
			SellerId = sellerId,
			CategoryId = categoryId,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	#endregion
}
