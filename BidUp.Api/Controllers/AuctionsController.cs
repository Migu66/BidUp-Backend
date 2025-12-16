using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Domain.Interfaces;

namespace BidUp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
	private readonly IAuctionService _auctionService;
	private readonly IBidService _bidService;
	private readonly ILogger<AuctionsController> _logger;

	public AuctionsController(
		IAuctionService auctionService,
		IBidService bidService,
		ILogger<AuctionsController> logger)
	{
		_auctionService = auctionService;
		_bidService = bidService;
		_logger = logger;
	}

	/// <summary>
	/// Obtener subastas activas
	/// </summary>
	[HttpGet]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<AuctionDto>>>> GetActiveAuctions(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		var auctions = await _auctionService.GetActiveAuctionsAsync(page, pageSize);

		return Ok(new ApiResponseDto<IEnumerable<AuctionDto>>
		{
			Success = true,
			Data = auctions,
			Message = "Subastas obtenidas correctamente"
		});
	}

	/// <summary>
	/// Obtener una subasta por ID
	/// </summary>
	[HttpGet("{id:guid}")]
	public async Task<ActionResult<ApiResponseDto<AuctionDto>>> GetById(Guid id)
	{
		var auction = await _auctionService.GetByIdAsync(id);

		if (auction == null)
		{
			return NotFound(new ApiResponseDto<AuctionDto>
			{
				Success = false,
				Message = "Subasta no encontrada"
			});
		}

		return Ok(new ApiResponseDto<AuctionDto>
		{
			Success = true,
			Data = auction
		});
	}

	/// <summary>
	/// Obtener subastas por categoría
	/// </summary>
	[HttpGet("category/{categoryId:guid}")]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<AuctionDto>>>> GetByCategory(
		Guid categoryId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		var auctions = await _auctionService.GetAuctionsByCategoryAsync(categoryId, page, pageSize);

		return Ok(new ApiResponseDto<IEnumerable<AuctionDto>>
		{
			Success = true,
			Data = auctions
		});
	}

	/// <summary>
	/// Crear una nueva subasta (requiere autenticación)
	/// </summary>
	[Authorize]
	[HttpPost]
	public async Task<ActionResult<ApiResponseDto<AuctionDto>>> Create([FromBody] CreateAuctionDto dto)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(new ApiResponseDto<AuctionDto>
			{
				Success = false,
				Message = "Datos inválidos",
				Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
			});
		}

		var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized(new ApiResponseDto<AuctionDto>
			{
				Success = false,
				Message = "Usuario no autenticado"
			});
		}

		try
		{
			var auction = await _auctionService.CreateAuctionAsync(dto, Guid.Parse(userId));

			return CreatedAtAction(
				nameof(GetById),
				new { id = auction.Id },
				new ApiResponseDto<AuctionDto>
				{
					Success = true,
					Data = auction,
					Message = "Subasta creada correctamente"
				});
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new ApiResponseDto<AuctionDto>
			{
				Success = false,
				Message = ex.Message
			});
		}
	}

	/// <summary>
	/// Cancelar una subasta (solo el vendedor, sin pujas)
	/// </summary>
	[Authorize]
	[HttpDelete("{id:guid}")]
	public async Task<ActionResult<ApiResponseDto<object>>> Cancel(Guid id)
	{
		var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized(new ApiResponseDto<object>
			{
				Success = false,
				Message = "Usuario no autenticado"
			});
		}

		try
		{
			var result = await _auctionService.CancelAuctionAsync(id, Guid.Parse(userId));

			if (!result)
			{
				return NotFound(new ApiResponseDto<object>
				{
					Success = false,
					Message = "Subasta no encontrada o no tienes permisos"
				});
			}

			return Ok(new ApiResponseDto<object>
			{
				Success = true,
				Message = "Subasta cancelada correctamente"
			});
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new ApiResponseDto<object>
			{
				Success = false,
				Message = ex.Message
			});
		}
	}

	/// <summary>
	/// Obtener historial de pujas de una subasta
	/// </summary>
	[HttpGet("{id:guid}/bids")]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<BidDto>>>> GetBids(
		Guid id,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 50)
	{
		var bids = await _auctionService.GetAuctionBidsAsync(id, page, pageSize);

		return Ok(new ApiResponseDto<IEnumerable<BidDto>>
		{
			Success = true,
			Data = bids
		});
	}

	/// <summary>
	/// Colocar una puja (requiere autenticación)
	/// </summary>
	[Authorize]
	[HttpPost("{id:guid}/bids")]
	public async Task<ActionResult<ApiResponseDto<BidDto>>> PlaceBid(Guid id, [FromBody] PlaceBidDto dto)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(new ApiResponseDto<BidDto>
			{
				Success = false,
				Message = "Datos inválidos",
				Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
			});
		}

		var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized(new ApiResponseDto<BidDto>
			{
				Success = false,
				Message = "Usuario no autenticado"
			});
		}

		// Obtener IP para auditoría y rate limiting
		var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

		var result = await _bidService.PlaceBidAsync(id, Guid.Parse(userId), dto.Amount, ipAddress);

		if (!result.Success)
		{
			return BadRequest(new ApiResponseDto<BidDto>
			{
				Success = false,
				Message = result.ErrorMessage ?? "Error al procesar la puja"
			});
		}

		return Ok(new ApiResponseDto<BidDto>
		{
			Success = true,
			Data = result.Bid,
			Message = "Puja realizada correctamente"
		});
	}

	/// <summary>
	/// Obtener mis subastas (como vendedor)
	/// </summary>
	[Authorize]
	[HttpGet("my-auctions")]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<AuctionDto>>>> GetMyAuctions(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized(new ApiResponseDto<IEnumerable<AuctionDto>>
			{
				Success = false,
				Message = "Usuario no autenticado"
			});
		}

		var auctions = await _auctionService.GetAuctionsBySellerAsync(Guid.Parse(userId), page, pageSize);

		return Ok(new ApiResponseDto<IEnumerable<AuctionDto>>
		{
			Success = true,
			Data = auctions
		});
	}

	/// <summary>
	/// Obtener mis pujas
	/// </summary>
	[Authorize]
	[HttpGet("my-bids")]
	public async Task<ActionResult<ApiResponseDto<IEnumerable<BidDto>>>> GetMyBids(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized(new ApiResponseDto<IEnumerable<BidDto>>
			{
				Success = false,
				Message = "Usuario no autenticado"
			});
		}

		var bids = await _bidService.GetUserBidsAsync(Guid.Parse(userId), page, pageSize);

		return Ok(new ApiResponseDto<IEnumerable<BidDto>>
		{
			Success = true,
			Data = bids
		});
	}
}
