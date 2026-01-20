using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Application.DTOs.Common;
using BidUp.Api.Domain.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace BidUp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Consumes("application/json")]
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
	[SwaggerOperation(
		Summary = "Listar subastas activas",
		Description = "Devuelve subastas activas ordenadas por tiempo restante.",
		Tags = new[] { "Auctions" })]
	[HttpGet]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<AuctionDto>>), StatusCodes.Status200OK)]
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
	[SwaggerOperation(
		Summary = "Detalle de subasta",
		Description = "Incluye vendedor, categoría y última puja.",
		Tags = new[] { "Auctions" })]
	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status404NotFound)]
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
	[SwaggerOperation(
		Summary = "Subastas por categoría",
		Description = "Lista subastas activas filtradas por categoría.",
		Tags = new[] { "Auctions" })]
	[HttpGet("category/{categoryId:guid}")]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<AuctionDto>>), StatusCodes.Status200OK)]
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
	[SwaggerOperation(
		Summary = "Crear subasta",
		Description = "Crea una subasta con título, descripción, precios y tiempos.",
		Tags = new[] { "Auctions" })]
	[Authorize]
	[HttpPost]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status401Unauthorized)]
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
	/// Activar una subasta pendiente (solo el vendedor)
	/// </summary>
	[SwaggerOperation(
		Summary = "Activar subasta",
		Description = "Activa una subasta que está en estado Pending. Solo el vendedor puede activarla.",
		Tags = new[] { "Auctions" })]
	[Authorize]
	[HttpPost("{id:guid}/activate")]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ApiResponseDto<AuctionDto>), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ApiResponseDto<AuctionDto>>> ActivateAuction(Guid id)
	{
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
			var auction = await _auctionService.ActivateAuctionAsync(id, Guid.Parse(userId));

			return Ok(new ApiResponseDto<AuctionDto>
			{
				Success = true,
				Data = auction,
				Message = "Subasta activada correctamente"
			});
		}
		catch (InvalidOperationException ex)
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
	[SwaggerOperation(
		Summary = "Cancelar subasta",
		Description = "Solo permitido si no existen pujas.",
		Tags = new[] { "Auctions" })]
	[Authorize]
	[HttpDelete("{id:guid}")]
	[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
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
	[SwaggerOperation(
		Summary = "Historial de pujas",
		Description = "Lista las pujas ordenadas por timestamp descendente.",
		Tags = new[] { "Bids" })]
	[HttpGet("{id:guid}/bids")]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<BidDto>>), StatusCodes.Status200OK)]
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
	/// <remarks>
	/// Concurrencia: el backend usa locks (Redis SETNX/Lua) y timestamp del servidor.\n
	/// Rate limiting: 10 pujas por minuto por usuario.
	/// </remarks>
	[SwaggerOperation(
		Summary = "Pujar",
		Description = "Realiza una puja válida y notifica en tiempo real.",
		Tags = new[] { "Bids" })]
	[Authorize]
	[HttpPost("{id:guid}/bids")]
	[ProducesResponseType(typeof(ApiResponseDto<BidDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<BidDto>), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ApiResponseDto<BidDto>), StatusCodes.Status401Unauthorized)]
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
	[SwaggerOperation(
		Summary = "Mis subastas",
		Description = "Lista subastas creadas por el usuario.",
		Tags = new[] { "Auctions" })]
	[Authorize]
	[HttpGet("my-auctions")]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<AuctionDto>>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<AuctionDto>>), StatusCodes.Status401Unauthorized)]
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
	[SwaggerOperation(
		Summary = "Mis pujas",
		Description = "Lista pujas realizadas por el usuario.",
		Tags = new[] { "Bids" })]
	[Authorize]
	[HttpGet("my-bids")]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<BidDto>>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ApiResponseDto<IEnumerable<BidDto>>), StatusCodes.Status401Unauthorized)]
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
