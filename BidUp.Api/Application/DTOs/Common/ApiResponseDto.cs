namespace BidUp.Api.Application.DTOs.Common;

public class ApiResponseDto<T>
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public T? Data { get; set; }
	public List<string>? Errors { get; set; }

	public static ApiResponseDto<T> SuccessResponse(T data, string message = "Operación exitosa")
	{
		return new ApiResponseDto<T>
		{
			Success = true,
			Message = message,
			Data = data
		};
	}

	public static ApiResponseDto<T> ErrorResponse(string message, List<string>? errors = null)
	{
		return new ApiResponseDto<T>
		{
			Success = false,
			Message = message,
			Errors = errors
		};
	}
}

public class ApiResponseDto
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public List<string>? Errors { get; set; }

	public static ApiResponseDto SuccessResponse(string message = "Operación exitosa")
	{
		return new ApiResponseDto
		{
			Success = true,
			Message = message
		};
	}

	public static ApiResponseDto ErrorResponse(string message, List<string>? errors = null)
	{
		return new ApiResponseDto
		{
			Success = false,
			Message = message,
			Errors = errors
		};
	}
}
