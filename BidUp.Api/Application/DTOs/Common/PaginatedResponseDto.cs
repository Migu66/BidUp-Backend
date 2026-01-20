namespace BidUp.Api.Application.DTOs.Common;

/// <summary>
/// Respuesta paginada genérica para infinite scroll
/// </summary>
/// <typeparam name="T">Tipo de datos en la colección</typeparam>
public class PaginatedResponseDto<T>
{
	/// <summary>
	/// Indica si la operación fue exitosa
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Datos de la página actual
	/// </summary>
	public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();

	/// <summary>
	/// Número total de elementos (antes de paginar)
	/// </summary>
	public int TotalCount { get; set; }

	/// <summary>
	/// Indica si hay más páginas disponibles
	/// </summary>
	public bool HasMore { get; set; }

	/// <summary>
	/// Mensaje opcional
	/// </summary>
	public string? Message { get; set; }

	/// <summary>
	/// Errores opcionales
	/// </summary>
	public List<string>? Errors { get; set; }
}
