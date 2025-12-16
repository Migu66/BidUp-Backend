using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;

namespace BidUp.Api.Controllers;

public class CategoryDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ImageUrl { get; set; }
	public int AuctionCount { get; set; }
}

public class CreateCategoryDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ImageUrl { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
	private readonly ApplicationDbContext _context;

	public CategoriesController(ApplicationDbContext context)
	{
		_context = context;
	}

	/// <summary>
	/// Obtener todas las categorías
	/// </summary>
	[HttpGet]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<CategoryDto>>>> GetAll()
	{
		var categories = await _context.Categories
			.Where(c => c.IsActive)
			.Select(c => new CategoryDto
			{
				Id = c.Id,
				Name = c.Name,
				Description = c.Description,
				ImageUrl = c.ImageUrl,
				AuctionCount = c.Auctions.Count(a => a.Status == Domain.Enums.AuctionStatus.Active)
			})
			.ToListAsync();

		return Ok(new ApiResponseDto<IEnumerable<CategoryDto>>
		{
			Success = true,
			Data = categories
		});
	}

	/// <summary>
	/// Obtener una categoría por ID
	/// </summary>
	[HttpGet("{id:guid}")]
	public async Task<ActionResult<ApiResponseDto<CategoryDto>>> GetById(Guid id)
	{
		var category = await _context.Categories
			.Where(c => c.Id == id)
			.Select(c => new CategoryDto
			{
				Id = c.Id,
				Name = c.Name,
				Description = c.Description,
				ImageUrl = c.ImageUrl,
				AuctionCount = c.Auctions.Count(a => a.Status == Domain.Enums.AuctionStatus.Active)
			})
			.FirstOrDefaultAsync();

		if (category == null)
		{
			return NotFound(new ApiResponseDto<CategoryDto>
			{
				Success = false,
				Message = "Categoría no encontrada"
			});
		}

		return Ok(new ApiResponseDto<CategoryDto>
		{
			Success = true,
			Data = category
		});
	}

	/// <summary>
	/// Crear una nueva categoría (requiere autenticación - TODO: solo admin)
	/// </summary>
	[Authorize]
	[HttpPost]
	public async Task<ActionResult<ApiResponseDto<CategoryDto>>> Create([FromBody] CreateCategoryDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Name))
		{
			return BadRequest(new ApiResponseDto<CategoryDto>
			{
				Success = false,
				Message = "El nombre es requerido"
			});
		}

		var exists = await _context.Categories.AnyAsync(c => c.Name == dto.Name);
		if (exists)
		{
			return BadRequest(new ApiResponseDto<CategoryDto>
			{
				Success = false,
				Message = "Ya existe una categoría con ese nombre"
			});
		}

		var category = new Category
		{
			Id = Guid.NewGuid(),
			Name = dto.Name,
			Description = dto.Description,
			ImageUrl = dto.ImageUrl,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		_context.Categories.Add(category);
		await _context.SaveChangesAsync();

		return CreatedAtAction(nameof(GetById), new { id = category.Id }, new ApiResponseDto<CategoryDto>
		{
			Success = true,
			Data = new CategoryDto
			{
				Id = category.Id,
				Name = category.Name,
				Description = category.Description,
				ImageUrl = category.ImageUrl,
				AuctionCount = 0
			},
			Message = "Categoría creada correctamente"
		});
	}
}
