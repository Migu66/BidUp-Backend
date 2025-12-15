var builder = WebApplication.CreateBuilder(args);

// Load environment variables
var dotenv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(dotenv))
{
	var lines = File.ReadAllLines(dotenv);
	foreach (var line in lines)
	{
		if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
		{
			var parts = line.Split('=', 2);
			if (parts.Length == 2)
			{
				Environment.SetEnvironmentVariable(parts[0], parts[1]);
			}
		}
	}
}

// Add services to the container.
builder.Services.AddOpenApi();

// Get connection string from environment variables or appsettings
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
	?? builder.Configuration.GetConnectionString("DefaultConnection");
var jwtSecret = Environment.GetEnvironmentVariable("Jwt__SecretKey")
	?? builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrEmpty(connectionString))
{
	Console.WriteLine("⚠️ WARNING: ConnectionString not configured. Using Development defaults.");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
	"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
	var forecast = Enumerable.Range(1, 5).Select(index =>
		new WeatherForecast
		(
			DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
			Random.Shared.Next(-20, 55),
			summaries[Random.Shared.Next(summaries.Length)]
		))
		.ToArray();
	return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
	public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
