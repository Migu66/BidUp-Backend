namespace BidUp.Api.Domain.Exceptions;

public class ValidationException : Exception
{
	public List<string> Errors { get; }

	public ValidationException(string message) : base(message)
	{
		Errors = new List<string> { message };
	}

	public ValidationException(List<string> errors) : base("Se encontraron errores de validación")
	{
		Errors = errors;
	}
}
