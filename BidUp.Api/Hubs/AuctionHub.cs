using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BidUp.Api.Application.DTOs;
using BidUp.Api.Application.DTOs.Auction;
using BidUp.Api.Domain.Interfaces;
using System.Security.Claims;

namespace BidUp.Api.Hubs;

/// <summary>
/// Hub de SignalR para subastas en tiempo real.
/// Maneja la comunicación bidireccional entre servidor y clientes.
/// Permite conexiones anónimas para recibir actualizaciones en tiempo real.
/// Las acciones que modifican datos requieren autenticación con [Authorize].
/// </summary>
public class AuctionHub : Hub
{
	private static int _connectedUsersCount = 0;
	private readonly ILogger<AuctionHub> _logger;
	private readonly IBidService _bidService;
	private readonly IAuctionService _auctionService;

	public AuctionHub(ILogger<AuctionHub> logger, IBidService bidService, IAuctionService auctionService)
	{
		_logger = logger;
		_bidService = bidService;
		_auctionService = auctionService;
	}

	/// <summary>
	/// Verifica si el usuario actual está autenticado
	/// </summary>
	private bool IsAuthenticated => Context.User?.Identity?.IsAuthenticated ?? false;

	/// <summary>
	/// Obtiene el ID del usuario autenticado o null si es anónimo
	/// </summary>
	private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

	/// <summary>
	/// Se ejecuta cuando un cliente se conecta al Hub.
	/// Permite conexiones tanto autenticadas como anónimas.
	/// </summary>
	public override async Task OnConnectedAsync()
	{
		Interlocked.Increment(ref _connectedUsersCount);
		
		var userId = GetUserId();
		var connectionType = IsAuthenticated ? "Autenticado" : "Anónimo";

		_logger.LogInformation("Cliente conectado: {ConnectionId}, Usuario: {UserId}, Tipo: {ConnectionType}",
			Context.ConnectionId, userId ?? "Anónimo", connectionType);

		await BroadcastLiveStats();
		await base.OnConnectedAsync();
	}

	/// <summary>
	/// Se ejecuta cuando un cliente se desconecta del Hub
	/// </summary>
	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		Interlocked.Decrement(ref _connectedUsersCount);
		
		var userId = GetUserId();
		_logger.LogInformation("Cliente desconectado: {ConnectionId}, Usuario: {UserId}",
			Context.ConnectionId, userId ?? "Anónimo");

		if (exception != null)
		{
			_logger.LogError(exception, "Error en desconexión del cliente {ConnectionId}",
				Context.ConnectionId);
		}

		await BroadcastLiveStats();
		await base.OnDisconnectedAsync(exception);
	}

	/// <summary>
	/// Unirse a una sala de subasta específica para recibir actualizaciones.
	/// Permite usuarios anónimos para que puedan ver las subastas en tiempo real.
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	public async Task JoinAuction(Guid auctionId)
	{
		var groupName = GetAuctionGroupName(auctionId);
		await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

		var userId = GetUserId();
		var userType = IsAuthenticated ? "autenticado" : "anónimo";

		_logger.LogInformation("Usuario {UserId} ({UserType}) se unió a la subasta {AuctionId}",
			userId ?? Context.ConnectionId, userType, auctionId);

		// Notificar al cliente que se unió exitosamente
		await Clients.Caller.SendAsync("JoinedAuction", new
		{
			AuctionId = auctionId,
			Message = "Te has unido a la subasta",
			Timestamp = DateTime.UtcNow,
			IsAuthenticated = IsAuthenticated
		});
	}

	/// <summary>
	/// Salir de una sala de subasta.
	/// Permite usuarios anónimos.
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	public async Task LeaveAuction(Guid auctionId)
	{
		var groupName = GetAuctionGroupName(auctionId);
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

		var userId = GetUserId();
		_logger.LogInformation("Usuario {UserId} salió de la subasta {AuctionId}",
			userId ?? Context.ConnectionId, auctionId);

		await Clients.Caller.SendAsync("LeftAuction", new
		{
			AuctionId = auctionId,
			Message = "Has salido de la subasta",
			Timestamp = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Solicitar sincronización del timer de la subasta.
	/// Permite usuarios anónimos para mantener sincronizado el tiempo.
	/// </summary>
	/// <param name="auctionId">ID de la subasta</param>
	public async Task RequestTimerSync(Guid auctionId)
	{
		// Este método será llamado por el servicio de subastas para enviar la sincronización
		// Por ahora solo notificamos que recibimos la solicitud
		await Clients.Caller.SendAsync("TimerSyncRequested", new
		{
			AuctionId = auctionId,
			RequestedAt = DateTime.UtcNow
		});
	}

	#region Métodos que requieren autenticación

	/// <summary>
	/// Coloca una puja a través de SignalR.
	/// Requiere autenticación. Utiliza la conexión WebSocket existente para mayor eficiencia.
	/// Envía BidAccepted al caller si la puja fue exitosa, o BidError si falló.
	/// </summary>
	/// <param name="auctionId">ID de la subasta (string para facilitar uso desde el cliente)</param>
	/// <param name="amount">Monto de la puja</param>
	[Authorize]
	public async Task PlaceBid(string auctionId, decimal amount)
	{
		try
		{
			// Obtener userId del usuario autenticado
			var userId = GetUserId();
			if (string.IsNullOrEmpty(userId))
			{
				await Clients.Caller.SendAsync("BidError", "No autenticado. Debes iniciar sesión para pujar.");
				return;
			}

			// Validar que el auctionId sea un GUID válido
			if (!Guid.TryParse(auctionId, out var auctionGuid))
			{
				await Clients.Caller.SendAsync("BidError", "ID de subasta inválido.");
				return;
			}

			// Validar que el userId sea un GUID válido
			if (!Guid.TryParse(userId, out var userGuid))
			{
				await Clients.Caller.SendAsync("BidError", "ID de usuario inválido.");
				return;
			}

			// Validar monto positivo
			if (amount <= 0)
			{
				await Clients.Caller.SendAsync("BidError", "El monto de la puja debe ser mayor a cero.");
				return;
			}

			_logger.LogInformation(
				"Puja vía SignalR: Usuario {UserId}, Subasta {AuctionId}, Monto {Amount}",
				userId, auctionId, amount);

			// Usar la misma lógica que el endpoint HTTP
			// Pasamos null como IP ya que SignalR no proporciona IP directamente de la misma forma
			var ipAddress = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString();
			var result = await _bidService.PlaceBidAsync(auctionGuid, userGuid, amount, ipAddress);

			if (result.Success)
			{
				// Confirmar al pujador que su puja fue aceptada
				await Clients.Caller.SendAsync("BidAccepted", result.Bid);

				_logger.LogInformation(
					"Puja aceptada vía SignalR: Usuario {UserId}, Subasta {AuctionId}, Monto {Amount}",
					userId, auctionId, amount);

				// Nota: La notificación NewBid a todos los participantes ya se hace en BidService.NotifyNewBidAsync
			}
			else
			{
				// Enviar error solo al que pujó
				await Clients.Caller.SendAsync("BidError", result.ErrorMessage ?? "Error desconocido al procesar la puja.");

				_logger.LogWarning(
					"Puja rechazada vía SignalR: Usuario {UserId}, Subasta {AuctionId}, Monto {Amount}, Error: {Error}",
					userId, auctionId, amount, result.ErrorMessage);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error inesperado al procesar puja vía SignalR para subasta {AuctionId}", auctionId);
			await Clients.Caller.SendAsync("BidError", "Error interno al procesar la puja. Inténtalo de nuevo.");
		}
	}

	#endregion

	/// <summary>
	/// Difundir estadísticas en vivo a todos los clientes conectados
	/// </summary>
	private async Task BroadcastLiveStats()
	{
		var activeAuctions = await _auctionService.GetActiveAuctionsCountAsync();

		await Clients.All.SendAsync("LiveStatsUpdated", new LiveStatsDto
		{
			ActiveAuctions = activeAuctions,
			ConnectedUsers = _connectedUsersCount,
			Timestamp = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Obtener el nombre del grupo para una subasta
	/// </summary>
	private static string GetAuctionGroupName(Guid auctionId) => $"auction_{auctionId}";
}

/// <summary>
/// Extensiones para enviar notificaciones a través del Hub
/// </summary>
public static class AuctionHubExtensions
{
	/// <summary>
	/// Notificar nueva puja a todos los participantes de una subasta
	/// </summary>
	public static async Task NotifyNewBid(
		this IHubContext<AuctionHub> hubContext,
		Guid auctionId,
		BidNotificationDto notification)
	{
		await hubContext.Clients
			.Group($"auction_{auctionId}")
			.SendAsync("NewBid", notification);
	}

	/// <summary>
	/// Notificar a un usuario específico que ha sido superado
	/// </summary>
	public static async Task NotifyOutbid(
		this IHubContext<AuctionHub> hubContext,
		string userId,
		OutbidNotificationDto notification)
	{
		await hubContext.Clients
			.User(userId)
			.SendAsync("Outbid", notification);
	}

	/// <summary>
	/// Notificar cambio de estado de la subasta
	/// </summary>
	public static async Task NotifyAuctionStatusChange(
		this IHubContext<AuctionHub> hubContext,
		Guid auctionId,
		AuctionStatusNotificationDto notification)
	{
		await hubContext.Clients
			.Group($"auction_{auctionId}")
			.SendAsync("AuctionStatusChanged", notification);
	}

	/// <summary>
	/// Sincronizar el timer de la subasta con todos los clientes
	/// </summary>
	public static async Task SyncAuctionTimer(
		this IHubContext<AuctionHub> hubContext,
		Guid auctionId,
		AuctionTimerSyncDto timerSync)
	{
		await hubContext.Clients
			.Group($"auction_{auctionId}")
			.SendAsync("TimerSync", timerSync);
	}

	/// <summary>
	/// Notificar que la subasta ha terminado
	/// </summary>
	public static async Task NotifyAuctionEnded(
		this IHubContext<AuctionHub> hubContext,
		Guid auctionId,
		AuctionStatusNotificationDto notification)
	{
		await hubContext.Clients
			.Group($"auction_{auctionId}")
			.SendAsync("AuctionEnded", notification);
	}
}
