using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using BidUp.Api.Controllers;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Tests.Controllers;

public class AuctionsControllerTests
{
	private readonly Mock<IAuctionService> _mockAuctionService;
	private readonly Mock<IBidService> _mockBidService;
	private readonly Mock<ILogger<AuctionsController>> _mockLogger;
	private readonly AuctionsController _controller;

	public AuctionsControllerTests()
	{
		_mockAuctionService = new Mock<IAuctionService>();
		_mockBidService = new Mock<IBidService>();
		_mockLogger = new Mock<ILogger<AuctionsController>>();
		_controller = new AuctionsController(
			_mockAuctionService.Object,
			_mockBidService.Object,
			_mockLogger.Object);
	}

	#region GetActiveAuctions Tests

	[Fact]
	public async Task GetActiveAuctions_ReturnsOkWithAuctions()
	{
		// Arrange
		var auctions = new List<AuctionDto>
		{
			CreateTestAuctionDto("Laptop"),
			CreateTestAuctionDto("Phone")
		};

		_mockAuctionService.Setup(x => x.GetActiveAuctionsAsync(1, 20))
			.ReturnsAsync(auctions);

		// Act
		var result = await _controller.GetActiveAuctions();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<AuctionDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().HaveCount(2);
		response.Message.Should().Be("Subastas obtenidas correctamente");
	}

	[Fact]
	public async Task GetActiveAuctions_WithPagination_PassesCorrectParameters()
	{
		// Arrange
		var auctions = new List<AuctionDto>();
		_mockAuctionService.Setup(x => x.GetActiveAuctionsAsync(2, 10))
			.ReturnsAsync(auctions);

		// Act
		await _controller.GetActiveAuctions(page: 2, pageSize: 10);

		// Assert
		_mockAuctionService.Verify(x => x.GetActiveAuctionsAsync(2, 10), Times.Once);
	}

	#endregion

	#region GetById Tests

	[Fact]
	public async Task GetById_WithExistingId_ReturnsOk()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var auction = CreateTestAuctionDto("Test Auction");
		auction.Id = auctionId;

		_mockAuctionService.Setup(x => x.GetByIdAsync(auctionId))
			.ReturnsAsync(auction);

		// Act
		var result = await _controller.GetById(auctionId);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.Id.Should().Be(auctionId);
	}

	[Fact]
	public async Task GetById_WithNonExistingId_ReturnsNotFound()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		_mockAuctionService.Setup(x => x.GetByIdAsync(auctionId))
			.ReturnsAsync((AuctionDto?)null);

		// Act
		var result = await _controller.GetById(auctionId);

		// Assert
		var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
		var response = notFoundResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Subasta no encontrada");
	}

	#endregion

	#region GetByCategory Tests

	[Fact]
	public async Task GetByCategory_ReturnsAuctionsForCategory()
	{
		// Arrange
		var categoryId = Guid.NewGuid();
		var auctions = new List<AuctionDto>
		{
			CreateTestAuctionDto("Laptop"),
			CreateTestAuctionDto("Phone")
		};

		_mockAuctionService.Setup(x => x.GetAuctionsByCategoryAsync(categoryId, 1, 20))
			.ReturnsAsync(auctions);

		// Act
		var result = await _controller.GetByCategory(categoryId);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<AuctionDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().HaveCount(2);
	}

	#endregion

	#region Create Tests

	[Fact]
	public async Task Create_WithValidData_ReturnsCreated()
	{
		// Arrange
		var userId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		var createDto = new CreateAuctionDto
		{
			Title = "New Auction",
			Description = "Test auction description for testing purposes",
			StartingPrice = 100,
			MinBidIncrement = 5,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = Guid.NewGuid()
		};

		var createdAuction = CreateTestAuctionDto("New Auction");
		_mockAuctionService.Setup(x => x.CreateAuctionAsync(createDto, userId))
			.ReturnsAsync(createdAuction);

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Subasta creada correctamente");
		response.Data.Should().NotBeNull();
	}

	[Fact]
	public async Task Create_WithInvalidModelState_ReturnsBadRequest()
	{
		// Arrange
		var userId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		var createDto = new CreateAuctionDto();
		_controller.ModelState.AddModelError("Title", "El título es requerido");

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Datos inválidos");
	}

	[Fact]
	public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
	{
		// Arrange
		SetupUnauthenticatedUser();

		var createDto = new CreateAuctionDto
		{
			Title = "Test",
			Description = "Test auction description for testing purposes",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			CategoryId = Guid.NewGuid()
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Usuario no autenticado");
	}

	[Fact]
	public async Task Create_WithInvalidDates_ReturnsBadRequest()
	{
		// Arrange
		var userId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		var createDto = new CreateAuctionDto
		{
			Title = "Invalid Dates",
			Description = "Test auction description for testing purposes",
			StartingPrice = 100,
			StartTime = DateTime.UtcNow.AddDays(7),
			EndTime = DateTime.UtcNow.AddDays(1),
			CategoryId = Guid.NewGuid()
		};

		_mockAuctionService.Setup(x => x.CreateAuctionAsync(createDto, userId))
			.ThrowsAsync(new ArgumentException("La fecha de fin debe ser posterior a la fecha de inicio."));

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<AuctionDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("La fecha de fin debe ser posterior a la fecha de inicio.");
	}

	#endregion

	#region Cancel Tests

	[Fact]
	public async Task Cancel_WithValidAuction_ReturnsOk()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		_mockAuctionService.Setup(x => x.CancelAuctionAsync(auctionId, userId))
			.ReturnsAsync(true);

		// Act
		var result = await _controller.Cancel(auctionId);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<object>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Subasta cancelada correctamente");
	}

	[Fact]
	public async Task Cancel_WithNonExistentAuction_ReturnsNotFound()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		_mockAuctionService.Setup(x => x.CancelAuctionAsync(auctionId, userId))
			.ReturnsAsync(false);

		// Act
		var result = await _controller.Cancel(auctionId);

		// Assert
		var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
		var response = notFoundResult.Value.Should().BeOfType<ApiResponseDto<object>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Subasta no encontrada o no tienes permisos");
	}

	[Fact]
	public async Task Cancel_WithoutAuthentication_ReturnsUnauthorized()
	{
		// Arrange
		SetupUnauthenticatedUser();
		var auctionId = Guid.NewGuid();

		// Act
		var result = await _controller.Cancel(auctionId);

		// Assert
		var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto<object>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Usuario no autenticado");
	}

	[Fact]
	public async Task Cancel_WithBids_ReturnsBadRequest()
	{
		// Arrange
		var userId = Guid.NewGuid();
		var auctionId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		_mockAuctionService.Setup(x => x.CancelAuctionAsync(auctionId, userId))
			.ThrowsAsync(new InvalidOperationException("No se puede cancelar una subasta con pujas."));

		// Act
		var result = await _controller.Cancel(auctionId);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<object>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("No se puede cancelar una subasta con pujas.");
	}

	#endregion

	#region GetBids Tests

	[Fact]
	public async Task GetBids_ReturnsBidsForAuction()
	{
		// Arrange
		var auctionId = Guid.NewGuid();
		var bids = new List<BidDto>
		{
			new BidDto { Id = Guid.NewGuid(), Amount = 110, AuctionId = auctionId },
			new BidDto { Id = Guid.NewGuid(), Amount = 120, AuctionId = auctionId }
		};

		_mockAuctionService.Setup(x => x.GetAuctionBidsAsync(auctionId, 1, 50))
			.ReturnsAsync(bids);

		// Act
		var result = await _controller.GetBids(auctionId);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<BidDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().HaveCount(2);
	}

	#endregion

	#region GetMyAuctions Tests

	[Fact]
	public async Task GetMyAuctions_WithAuthenticatedUser_ReturnsUserAuctions()
	{
		// Arrange
		var userId = Guid.NewGuid();
		SetupAuthenticatedUser(userId);

		var auctions = new List<AuctionDto>
		{
			CreateTestAuctionDto("My Auction 1"),
			CreateTestAuctionDto("My Auction 2")
		};

		_mockAuctionService.Setup(x => x.GetAuctionsBySellerAsync(userId, 1, 20))
			.ReturnsAsync(auctions);

		// Act
		var result = await _controller.GetMyAuctions();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<AuctionDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetMyAuctions_WithoutAuthentication_ReturnsUnauthorized()
	{
		// Arrange
		SetupUnauthenticatedUser();

		// Act
		var result = await _controller.GetMyAuctions();

		// Assert
		var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
		var response = unauthorizedResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<AuctionDto>>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Usuario no autenticado");
	}

	#endregion

	#region Helper Methods

	private AuctionDto CreateTestAuctionDto(string title)
	{
		return new AuctionDto
		{
			Id = Guid.NewGuid(),
			Title = title,
			Description = "Test description",
			StartingPrice = 100,
			CurrentPrice = 100,
			MinBidIncrement = 5,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddDays(7),
			Status = "Active",
			TotalBids = 0,
			TimeRemaining = TimeSpan.FromDays(7),
			SellerId = Guid.NewGuid(),
			SellerName = "Test Seller",
			CategoryId = Guid.NewGuid(),
			CategoryName = "Test Category"
		};
	}

	private void SetupAuthenticatedUser(Guid userId)
	{
		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
			new Claim(ClaimTypes.Email, "test@example.com")
		};

		var identity = new ClaimsIdentity(claims, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		_controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = principal
			}
		};
	}

	private void SetupUnauthenticatedUser()
	{
		var identity = new ClaimsIdentity();
		var principal = new ClaimsPrincipal(identity);

		_controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = principal
			}
		};
	}

	#endregion
}
