using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Net;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Domain.Interfaces;
using BidUp.Api.Hubs;

namespace BidUp.Tests.Hubs;

public class AuctionHubTests
{
	private readonly Mock<ILogger<AuctionHub>> _mockLogger;
	private readonly Mock<IBidService> _mockBidService;
	private readonly Mock<HubCallerContext> _mockContext;
	private readonly Mock<IHubCallerClients> _mockClients;
	private readonly Mock<IGroupManager> _mockGroups;
	private readonly Mock<ISingleClientProxy> _mockClientProxy;
	private readonly AuctionHub _hub;

	public AuctionHubTests()
	{
		_mockLogger = new Mock<ILogger<AuctionHub>>();
		_mockBidService = new Mock<IBidService>();
		_mockContext = new Mock<HubCallerContext>();
		_mockClients = new Mock<IHubCallerClients>();
		_mockGroups = new Mock<IGroupManager>();
		_mockClientProxy = new Mock<ISingleClientProxy>();

		_hub = new AuctionHub(_mockLogger.Object, _mockBidService.Object)
		{
			Context = _mockContext.Object,
			Clients = _mockClients.Object,
			Groups = _mockGroups.Object
		};

		// Setup básico para Caller
		_mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
	}

	#region OnConnectedAsync Tests

	[Fact]
	public async Task OnConnectedAsync_LogsConnectionWithUserId()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.OnConnectedAsync();

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cliente conectado")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task OnConnectedAsync_WithAnonymousUser_LogsAnonymous()
	{
		// Arrange
		var connectionId = "test-connection-123";
		SetupUnauthenticatedUser(connectionId);

		// Act
		await _hub.OnConnectedAsync();

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Anónimo")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region OnDisconnectedAsync Tests

	[Fact]
	public async Task OnDisconnectedAsync_LogsDisconnectionWithUserId()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.OnDisconnectedAsync(null);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cliente desconectado")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task OnDisconnectedAsync_WithException_LogsError()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		var exception = new Exception("Test exception");
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.OnDisconnectedAsync(exception);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error en desconexión")),
				exception,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region JoinAuction Tests

	[Fact]
	public async Task JoinAuction_AddsConnectionToGroup()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.JoinAuction(auctionId);

		// Assert
		_mockGroups.Verify(
			x => x.AddToGroupAsync(connectionId, $"auction_{auctionId}", default),
			Times.Once);
	}

	[Fact]
	public async Task JoinAuction_SendsConfirmationToCaller()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.JoinAuction(auctionId);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"JoinedAuction",
				It.Is<object[]>(o => o.Length == 1),
				default),
			Times.Once);
	}

	[Fact]
	public async Task JoinAuction_LogsUserAction()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.JoinAuction(auctionId);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("se unió a la subasta")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region LeaveAuction Tests

	[Fact]
	public async Task LeaveAuction_RemovesConnectionFromGroup()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.LeaveAuction(auctionId);

		// Assert
		_mockGroups.Verify(
			x => x.RemoveFromGroupAsync(connectionId, $"auction_{auctionId}", default),
			Times.Once);
	}

	[Fact]
	public async Task LeaveAuction_SendsConfirmationToCaller()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.LeaveAuction(auctionId);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"LeftAuction",
				It.Is<object[]>(o => o.Length == 1),
				default),
			Times.Once);
	}

	[Fact]
	public async Task LeaveAuction_LogsUserAction()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.LeaveAuction(auctionId);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("salió de la subasta")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region RequestTimerSync Tests

	[Fact]
	public async Task RequestTimerSync_SendsResponseToCaller()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);

		// Act
		await _hub.RequestTimerSync(auctionId);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"TimerSyncRequested",
				It.Is<object[]>(o => o.Length == 1),
				default),
			Times.Once);
	}

	#endregion

	#region Extension Methods Tests

	[Fact]
	public async Task NotifyNewBid_SendsToAuctionGroup()
	{
		// Arrange
		var mockHubContext = new Mock<IHubContext<AuctionHub>>();
		var mockClients = new Mock<IHubClients>();
		var mockGroupProxy = new Mock<IClientProxy>();

		var auctionId = Guid.NewGuid();
		var notification = new BidNotificationDto
		{
			AuctionId = auctionId,
			Bid = new BidDto
			{
				Id = Guid.NewGuid(),
				Amount = 150,
				BidderId = Guid.NewGuid(),
				BidderName = "Test Bidder",
				AuctionId = auctionId,
				Timestamp = DateTime.UtcNow
			},
			NewCurrentPrice = 150,
			TotalBids = 5,
			TimeRemaining = TimeSpan.FromHours(2)
		};

		mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.Group($"auction_{auctionId}")).Returns(mockGroupProxy.Object);

		// Act
		await mockHubContext.Object.NotifyNewBid(auctionId, notification);

		// Assert
		mockGroupProxy.Verify(
			x => x.SendCoreAsync(
				"NewBid",
				It.Is<object[]>(o => o.Length == 1 && o[0] == notification),
				default),
			Times.Once);
	}

	[Fact]
	public async Task NotifyOutbid_SendsToSpecificUser()
	{
		// Arrange
		var mockHubContext = new Mock<IHubContext<AuctionHub>>();
		var mockClients = new Mock<IHubClients>();
		var mockUserProxy = new Mock<IClientProxy>();

		var userId = Guid.NewGuid().ToString();
		var notification = new OutbidNotificationDto
		{
			AuctionId = Guid.NewGuid(),
			AuctionTitle = "Test Auction",
			YourBid = 100,
			NewHighestBid = 150,
			MinimumNextBid = 160
		};

		mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.User(userId)).Returns(mockUserProxy.Object);

		// Act
		await mockHubContext.Object.NotifyOutbid(userId, notification);

		// Assert
		mockUserProxy.Verify(
			x => x.SendCoreAsync(
				"Outbid",
				It.Is<object[]>(o => o.Length == 1 && o[0] == notification),
				default),
			Times.Once);
	}

	[Fact]
	public async Task NotifyAuctionStatusChange_SendsToAuctionGroup()
	{
		// Arrange
		var mockHubContext = new Mock<IHubContext<AuctionHub>>();
		var mockClients = new Mock<IHubClients>();
		var mockGroupProxy = new Mock<IClientProxy>();

		var auctionId = Guid.NewGuid();
		var notification = new AuctionStatusNotificationDto
		{
			AuctionId = auctionId,
			Status = "Completed",
			Message = "La subasta ha terminado"
		};

		mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.Group($"auction_{auctionId}")).Returns(mockGroupProxy.Object);

		// Act
		await mockHubContext.Object.NotifyAuctionStatusChange(auctionId, notification);

		// Assert
		mockGroupProxy.Verify(
			x => x.SendCoreAsync(
				"AuctionStatusChanged",
				It.Is<object[]>(o => o.Length == 1 && o[0] == notification),
				default),
			Times.Once);
	}

	[Fact]
	public async Task SyncAuctionTimer_SendsToAuctionGroup()
	{
		// Arrange
		var mockHubContext = new Mock<IHubContext<AuctionHub>>();
		var mockClients = new Mock<IHubClients>();
		var mockGroupProxy = new Mock<IClientProxy>();

		var auctionId = Guid.NewGuid();
		var timerSync = new AuctionTimerSyncDto
		{
			AuctionId = auctionId,
			EndTime = DateTime.UtcNow.AddHours(5),
			TimeRemaining = TimeSpan.FromHours(5),
			ServerTime = DateTime.UtcNow
		};

		mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.Group($"auction_{auctionId}")).Returns(mockGroupProxy.Object);

		// Act
		await mockHubContext.Object.SyncAuctionTimer(auctionId, timerSync);

		// Assert
		mockGroupProxy.Verify(
			x => x.SendCoreAsync(
				"TimerSync",
				It.Is<object[]>(o => o.Length == 1 && o[0] == timerSync),
				default),
			Times.Once);
	}

	[Fact]
	public async Task NotifyAuctionEnded_SendsToAuctionGroup()
	{
		// Arrange
		var mockHubContext = new Mock<IHubContext<AuctionHub>>();
		var mockClients = new Mock<IHubClients>();
		var mockGroupProxy = new Mock<IClientProxy>();

		var auctionId = Guid.NewGuid();
		var notification = new AuctionStatusNotificationDto
		{
			AuctionId = auctionId,
			Status = "Ended",
			Message = "La subasta ha finalizado",
			WinnerBid = new BidDto
			{
				Id = Guid.NewGuid(),
				Amount = 200,
				BidderId = Guid.NewGuid(),
				BidderName = "Winner",
				AuctionId = auctionId
			}
		};

		mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
		mockClients.Setup(c => c.Group($"auction_{auctionId}")).Returns(mockGroupProxy.Object);

		// Act
		await mockHubContext.Object.NotifyAuctionEnded(auctionId, notification);

		// Assert
		mockGroupProxy.Verify(
			x => x.SendCoreAsync(
				"AuctionEnded",
				It.Is<object[]>(o => o.Length == 1 && o[0] == notification),
				default),
			Times.Once);
	}

	#endregion

	#region Helper Methods

	private void SetupAuthenticatedUser(string userId, string connectionId)
	{
		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, userId)
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		_mockContext.Setup(c => c.User).Returns(principal);
		_mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
	}

	private void SetupUnauthenticatedUser(string connectionId)
	{
		var identity = new ClaimsIdentity();
		var principal = new ClaimsPrincipal(identity);

		_mockContext.Setup(c => c.User).Returns(principal);
		_mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
	}

	private void SetupHttpContext(string? ipAddress = "127.0.0.1")
	{
		var mockHttpContext = new Mock<HttpContext>();
		var mockConnection = new Mock<ConnectionInfo>();

		if (ipAddress != null)
		{
			mockConnection.Setup(c => c.RemoteIpAddress).Returns(IPAddress.Parse(ipAddress));
		}

		mockHttpContext.Setup(h => h.Connection).Returns(mockConnection.Object);
		_mockContext.Setup(c => c.GetHttpContext()).Returns(mockHttpContext.Object);
	}

	#endregion

	#region PlaceBid Tests

	[Fact]
	public async Task PlaceBid_WithUnauthenticatedUser_SendsBidError()
	{
		// Arrange
		var connectionId = "test-connection-123";
		var auctionId = Guid.NewGuid().ToString();
		SetupUnauthenticatedUser(connectionId);

		// Act
		await _hub.PlaceBid(auctionId, 100);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("No autenticado")),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WithInvalidAuctionId_SendsBidError()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		SetupAuthenticatedUser(userId, connectionId);
		SetupHttpContext();

		// Act
		await _hub.PlaceBid("invalid-guid", 100);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("ID de subasta inválido")),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WithNegativeAmount_SendsBidError()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		var auctionId = Guid.NewGuid().ToString();
		SetupAuthenticatedUser(userId, connectionId);
		SetupHttpContext();

		// Act
		await _hub.PlaceBid(auctionId, -50);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("mayor a cero")),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WithZeroAmount_SendsBidError()
	{
		// Arrange
		var userId = Guid.NewGuid().ToString();
		var connectionId = "test-connection-123";
		var auctionId = Guid.NewGuid().ToString();
		SetupAuthenticatedUser(userId, connectionId);
		SetupHttpContext();

		// Act
		await _hub.PlaceBid(auctionId, 0);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("mayor a cero")),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WhenBidServiceSucceeds_SendsBidAccepted()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 150m;

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext();

		var bidDto = new BidDto
		{
			Id = Guid.NewGuid(),
			Amount = amount,
			BidderId = userId,
			BidderName = "Test User",
			AuctionId = auctionId,
			Timestamp = DateTime.UtcNow,
			IsWinning = true
		};

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, It.IsAny<string?>()))
			.ReturnsAsync(BidResult.Succeeded(bidDto, amount));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidAccepted",
				It.Is<object[]>(o => o.Length == 1 && o[0] == bidDto),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WhenBidServiceFails_SendsBidError()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 50m;
		var errorMessage = "La puja mínima es $100.00";

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext();

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, It.IsAny<string?>()))
			.ReturnsAsync(BidResult.Failed(errorMessage));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == errorMessage),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_WhenBidServiceThrowsException_SendsBidError()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 100m;

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext();

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, It.IsAny<string?>()))
			.ThrowsAsync(new Exception("Database error"));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockClientProxy.Verify(
			x => x.SendCoreAsync(
				"BidError",
				It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("Error interno")),
				default),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_CallsBidServiceWithCorrectParameters()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 200m;
		var ipAddress = "192.168.1.100";

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext(ipAddress);

		var bidDto = new BidDto
		{
			Id = Guid.NewGuid(),
			Amount = amount,
			BidderId = userId,
			BidderName = "Test User",
			AuctionId = auctionId
		};

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, ipAddress))
			.ReturnsAsync(BidResult.Succeeded(bidDto, amount));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockBidService.Verify(
			s => s.PlaceBidAsync(auctionId, userId, amount, ipAddress),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_LogsSuccessfulBid()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 150m;

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext();

		var bidDto = new BidDto
		{
			Id = Guid.NewGuid(),
			Amount = amount,
			BidderId = userId,
			AuctionId = auctionId
		};

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, It.IsAny<string?>()))
			.ReturnsAsync(BidResult.Succeeded(bidDto, amount));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Puja aceptada vía SignalR")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task PlaceBid_LogsRejectedBid()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		var connectionId = "test-connection-123";
		var amount = 50m;

		SetupAuthenticatedUser(userId.ToString(), connectionId);
		SetupHttpContext();

		_mockBidService
			.Setup(s => s.PlaceBidAsync(auctionId, userId, amount, It.IsAny<string?>()))
			.ReturnsAsync(BidResult.Failed("Puja insuficiente"));

		// Act
		await _hub.PlaceBid(auctionId.ToString(), amount);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Puja rechazada vía SignalR")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion
}
