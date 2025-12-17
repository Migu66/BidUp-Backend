using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BidUp.Api.Controllers;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Enums;
using BidUp.Api.Application.DTOs.Common;

namespace BidUp.Tests.Controllers;

public class CategoriesControllerTests
{
	private readonly ApplicationDbContext _context;
	private readonly CategoriesController _controller;

	public CategoriesControllerTests()
	{
		// Configurar DbContext con InMemory
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;
		_context = new ApplicationDbContext(options);

		// Crear controlador
		_controller = new CategoriesController(_context);
	}

	#region GetAll Tests

	[Fact]
	public async Task GetAll_WithNoCategories_ReturnsEmptyList()
	{
		// Act
		var result = await _controller.GetAll();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<CategoryDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data.Should().BeEmpty();
	}

	[Fact]
	public async Task GetAll_WithActiveCategories_ReturnsAllActiveCategories()
	{
		// Arrange
		var categories = new List<Category>
		{
			new Category
			{
				Id = Guid.NewGuid(),
				Name = "Electrónica",
				Description = "Productos electrónicos",
				ImageUrl = "https://example.com/electronics.jpg",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			},
			new Category
			{
				Id = Guid.NewGuid(),
				Name = "Moda",
				Description = "Ropa y accesorios",
				ImageUrl = "https://example.com/fashion.jpg",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			},
			new Category
			{
				Id = Guid.NewGuid(),
				Name = "Hogar",
				Description = "Artículos para el hogar",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			}
		};

		_context.Categories.AddRange(categories);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetAll();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<CategoryDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data.Should().HaveCount(3);
		response.Data.Should().Contain(c => c.Name == "Electrónica");
		response.Data.Should().Contain(c => c.Name == "Moda");
		response.Data.Should().Contain(c => c.Name == "Hogar");
	}

	[Fact]
	public async Task GetAll_WithInactiveCategories_ReturnsOnlyActiveCategories()
	{
		// Arrange
		var categories = new List<Category>
		{
			new Category
			{
				Id = Guid.NewGuid(),
				Name = "Electrónica",
				Description = "Productos electrónicos",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			},
			new Category
			{
				Id = Guid.NewGuid(),
				Name = "Categoría Inactiva",
				Description = "Esta categoría está desactivada",
				IsActive = false,
				CreatedAt = DateTime.UtcNow
			}
		};

		_context.Categories.AddRange(categories);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetAll();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<CategoryDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data.Should().HaveCount(1);
		response.Data.Should().Contain(c => c.Name == "Electrónica");
		response.Data.Should().NotContain(c => c.Name == "Categoría Inactiva");
	}

	[Fact]
	public async Task GetAll_WithCategoriesWithAuctions_ReturnsCorrectAuctionCount()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			UserName = "testuser",
			Email = "test@example.com",
			FirstName = "Test",
			LastName = "User",
			IsActive = true
		};

		var category = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Electrónica",
			Description = "Productos electrónicos",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		var auctions = new List<Auction>
		{
			new Auction
			{
				Id = Guid.NewGuid(),
				Title = "Laptop HP",
				Description = "Laptop en buen estado",
				StartingPrice = 500,
				CurrentPrice = 500,
				StartTime = DateTime.UtcNow.AddDays(-1),
				EndTime = DateTime.UtcNow.AddDays(7),
				Status = AuctionStatus.Active,
				SellerId = user.Id,
				CategoryId = category.Id,
				CreatedAt = DateTime.UtcNow
			},
			new Auction
			{
				Id = Guid.NewGuid(),
				Title = "iPhone 13",
				Description = "iPhone usado",
				StartingPrice = 700,
				CurrentPrice = 700,
				StartTime = DateTime.UtcNow.AddDays(-2),
				EndTime = DateTime.UtcNow.AddDays(5),
				Status = AuctionStatus.Active,
				SellerId = user.Id,
				CategoryId = category.Id,
				CreatedAt = DateTime.UtcNow
			},
			new Auction
			{
				Id = Guid.NewGuid(),
				Title = "Tablet cerrada",
				Description = "Subasta cerrada",
				StartingPrice = 300,
				CurrentPrice = 350,
				StartTime = DateTime.UtcNow.AddDays(-10),
				EndTime = DateTime.UtcNow.AddDays(-1),
				Status = AuctionStatus.Completed,
				SellerId = user.Id,
				CategoryId = category.Id,
				CreatedAt = DateTime.UtcNow.AddDays(-10)
			}
		};

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(auctions);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetAll();

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<IEnumerable<CategoryDto>>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data.Should().HaveCount(1);

		var categoryDto = response.Data.First();
		categoryDto.Name.Should().Be("Electrónica");
		categoryDto.AuctionCount.Should().Be(2); // Solo las subastas activas
	}

	#endregion

	#region GetById Tests

	[Fact]
	public async Task GetById_WithExistingId_ReturnsCategory()
	{
		// Arrange
		var category = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Electrónica",
			Description = "Productos electrónicos",
			ImageUrl = "https://example.com/electronics.jpg",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetById(category.Id);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.Id.Should().Be(category.Id);
		response.Data.Name.Should().Be("Electrónica");
		response.Data.Description.Should().Be("Productos electrónicos");
		response.Data.ImageUrl.Should().Be("https://example.com/electronics.jpg");
	}

	[Fact]
	public async Task GetById_WithNonExistingId_ReturnsNotFound()
	{
		// Arrange
		var nonExistingId = Guid.NewGuid();

		// Act
		var result = await _controller.GetById(nonExistingId);

		// Assert
		var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
		var response = notFoundResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Categoría no encontrada");
		response.Data.Should().BeNull();
	}

	[Fact]
	public async Task GetById_WithInactiveCategory_ReturnsCategory()
	{
		// Arrange
		var category = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Categoría Inactiva",
			Description = "Esta está desactivada",
			IsActive = false,
			CreatedAt = DateTime.UtcNow
		};

		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetById(category.Id);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.Name.Should().Be("Categoría Inactiva");
	}

	[Fact]
	public async Task GetById_WithCategoryWithAuctions_ReturnsCorrectAuctionCount()
	{
		// Arrange
		var user = new User
		{
			Id = Guid.NewGuid(),
			UserName = "testuser",
			Email = "test@example.com",
			FirstName = "Test",
			LastName = "User",
			IsActive = true
		};

		var category = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Deportes",
			Description = "Artículos deportivos",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		var auctions = new List<Auction>
		{
			new Auction
			{
				Id = Guid.NewGuid(),
				Title = "Bicicleta",
				Description = "Bicicleta de montaña",
				StartingPrice = 200,
				CurrentPrice = 200,
				StartTime = DateTime.UtcNow.AddDays(-1),
				EndTime = DateTime.UtcNow.AddDays(5),
				Status = AuctionStatus.Active,
				SellerId = user.Id,
				CategoryId = category.Id,
				CreatedAt = DateTime.UtcNow
			},
			new Auction
			{
				Id = Guid.NewGuid(),
				Title = "Pelota",
				Description = "Pelota de fútbol",
				StartingPrice = 30,
				CurrentPrice = 35,
				StartTime = DateTime.UtcNow.AddDays(-5),
				EndTime = DateTime.UtcNow.AddDays(-1),
				Status = AuctionStatus.Completed,
				SellerId = user.Id,
				CategoryId = category.Id,
				CreatedAt = DateTime.UtcNow.AddDays(-5)
			}
		};

		_context.Users.Add(user);
		_context.Categories.Add(category);
		_context.Auctions.AddRange(auctions);
		await _context.SaveChangesAsync();

		// Act
		var result = await _controller.GetById(category.Id);

		// Assert
		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		var response = okResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.AuctionCount.Should().Be(1); // Solo la subasta activa
	}

	#endregion

	#region Create Tests

	[Fact]
	public async Task Create_WithValidData_ReturnsCreatedResult()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "Nueva Categoría",
			Description = "Descripción de prueba",
			ImageUrl = "https://example.com/image.jpg"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Message.Should().Be("Categoría creada correctamente");
		response.Data.Should().NotBeNull();
		response.Data!.Name.Should().Be("Nueva Categoría");
		response.Data.Description.Should().Be("Descripción de prueba");
		response.Data.ImageUrl.Should().Be("https://example.com/image.jpg");
		response.Data.AuctionCount.Should().Be(0);

		// Verificar que se creó en la base de datos
		var categoryInDb = await _context.Categories.FindAsync(response.Data.Id);
		categoryInDb.Should().NotBeNull();
		categoryInDb!.Name.Should().Be("Nueva Categoría");
		categoryInDb.IsActive.Should().BeTrue();
	}

	[Fact]
	public async Task Create_WithEmptyName_ReturnsBadRequest()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "",
			Description = "Descripción"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("El nombre es requerido");
		response.Data.Should().BeNull();

		// Verificar que no se creó en la base de datos
		var count = await _context.Categories.CountAsync();
		count.Should().Be(0);
	}

	[Fact]
	public async Task Create_WithWhitespaceName_ReturnsBadRequest()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "   ",
			Description = "Descripción"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("El nombre es requerido");
	}

	[Fact]
	public async Task Create_WithDuplicateName_ReturnsBadRequest()
	{
		// Arrange
		var existingCategory = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Electrónica",
			Description = "Productos electrónicos",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		_context.Categories.Add(existingCategory);
		await _context.SaveChangesAsync();

		var createDto = new CreateCategoryDto
		{
			Name = "Electrónica",
			Description = "Otra descripción"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
		var response = badRequestResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeFalse();
		response.Message.Should().Be("Ya existe una categoría con ese nombre");
		response.Data.Should().BeNull();

		// Verificar que solo existe una categoría
		var count = await _context.Categories.CountAsync();
		count.Should().Be(1);
	}

	[Fact]
	public async Task Create_WithNullDescription_CreatesSuccessfully()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "Categoría Sin Descripción",
			Description = null,
			ImageUrl = null
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.Name.Should().Be("Categoría Sin Descripción");
		response.Data.Description.Should().BeNull();
		response.Data.ImageUrl.Should().BeNull();
	}

	[Fact]
	public async Task Create_WithMinimalData_CreatesSuccessfully()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "Categoría Mínima"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;
		response.Success.Should().BeTrue();
		response.Data.Should().NotBeNull();
		response.Data!.Name.Should().Be("Categoría Mínima");

		// Verificar que el action name es correcto
		createdResult.ActionName.Should().Be(nameof(CategoriesController.GetById));
		createdResult.RouteValues.Should().ContainKey("id");
		createdResult.RouteValues!["id"].Should().Be(response.Data.Id);
	}

	[Fact]
	public async Task Create_SetsCorrectDefaultValues()
	{
		// Arrange
		var createDto = new CreateCategoryDto
		{
			Name = "Test Category"
		};

		// Act
		var result = await _controller.Create(createDto);

		// Assert
		var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
		var response = createdResult.Value.Should().BeOfType<ApiResponseDto<CategoryDto>>().Subject;

		var categoryInDb = await _context.Categories.FindAsync(response.Data!.Id);
		categoryInDb.Should().NotBeNull();
		categoryInDb!.IsActive.Should().BeTrue();
		categoryInDb.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	#endregion
}
