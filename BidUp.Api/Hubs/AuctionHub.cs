using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BidUp.Api.Application.DTOs.Auction;
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
	private readonly ILogger<AuctionHub> _logger;

	public AuctionHub(ILogger<AuctionHub> logger)
	{
		_logger = logger;
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
		var userId = GetUserId();
		var connectionType = IsAuthenticated ? "Autenticado" : "Anónimo";

		_logger.LogInformation("Cliente conectado: {ConnectionId}, Usuario: {UserId}, Tipo: {ConnectionType}",
			Context.ConnectionId, userId ?? "Anónimo", connectionType);

		await base.OnConnectedAsync();
	}

	/// <summary>
	/// Se ejecuta cuando un cliente se desconecta del Hub
	/// </summary>
	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var userId = GetUserId();
		_logger.LogInformation("Cliente desconectado: {ConnectionId}, Usuario: {UserId}",
			Context.ConnectionId, userId ?? "Anónimo");

		if (exception != null)
		{
			_logger.LogError(exception, "Error en desconexión del cliente {ConnectionId}",
				Context.ConnectionId);
		}

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

	// NOTA: Agregar aquí métodos que modifiquen datos con el atributo [Authorize]
	// Ejemplo:
	// [Authorize]
	// public async Task PlaceBidViaHub(Guid auctionId, decimal amount)
	// {
	//     // Implementación de puja en tiempo real
	// }

	#endregion

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
