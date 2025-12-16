using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Swashbuckle.AspNetCore.Annotations;
using StackExchange.Redis;
using AspNetCoreRateLimit;
using BidUp.Api.Application.Services;
using BidUp.Api.Configuration;
using BidUp.Api.Domain.Entities;
using BidUp.Api.Domain.Interfaces;
using BidUp.Api.Hubs;

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

// Get configuration from environment variables or appsettings
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
	?? builder.Configuration.GetConnectionString("DefaultConnection");
var jwtSecretKey = Environment.GetEnvironmentVariable("Jwt__SecretKey")
	?? builder.Configuration["Jwt:SecretKey"];
var jwtIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer")
	?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = Environment.GetEnvironmentVariable("Jwt__Audience")
	?? builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(connectionString))
{
	throw new InvalidOperationException("Connection string not configured. Set ConnectionStrings__DefaultConnection environment variable or appsettings value.");
}

if (string.IsNullOrEmpty(jwtSecretKey) || jwtSecretKey.Length < 32)
{
	Console.WriteLine("⚠️ WARNING: JWT SecretKey not configured or too short. Using Development defaults.");
	jwtSecretKey = "BidUpDevelopmentSecretKey_ChangeThisInProduction_MinLength32!";
}

// Configure JWT Settings
var jwtSettings = new JwtSettings
{
	SecretKey = jwtSecretKey,
	Issuer = jwtIssuer ?? "BidUpApi",
	Audience = jwtAudience ?? "BidUpClient",
	AccessTokenExpirationMinutes = 15,
	RefreshTokenExpirationDays = 7
};
builder.Services.Configure<JwtSettings>(options =>
{
	options.SecretKey = jwtSettings.SecretKey;
	options.Issuer = jwtSettings.Issuer;
	options.Audience = jwtSettings.Audience;
	options.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes;
	options.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
});

// Configure Redis
var redisConnectionString = Environment.GetEnvironmentVariable("Redis__ConnectionString")
	?? builder.Configuration["Redis:ConnectionString"]
	?? "localhost:6379";
var redisEnabled = bool.TryParse(
	Environment.GetEnvironmentVariable("Redis__Enabled") ?? builder.Configuration["Redis:Enabled"],
	out var enabled) && enabled;

if (redisEnabled)
{
	try
	{
		var redis = ConnectionMultiplexer.Connect(redisConnectionString);
		builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
		builder.Services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
		Console.WriteLine("✅ Redis conectado correctamente.");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"⚠️ No se pudo conectar a Redis: {ex.Message}. Usando locks en memoria.");
		builder.Services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
	}
}
else
{
	Console.WriteLine("ℹ️ Redis deshabilitado. Usando locks en memoria.");
	builder.Services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
}

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseNpgsql(connectionString));

// Configure Identity
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
	// Password settings
	options.Password.RequireDigit = true;
	options.Password.RequireLowercase = true;
	options.Password.RequireUppercase = true;
	options.Password.RequireNonAlphanumeric = true;
	options.Password.RequiredLength = 8;
	options.Password.RequiredUniqueChars = 4;

	// Lockout settings
	options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
	options.Lockout.MaxFailedAccessAttempts = 5;
	options.Lockout.AllowedForNewUsers = true;

	// User settings
	options.User.RequireUniqueEmail = true;
	options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

	// SignIn settings
	options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
	options.SaveToken = true;
	options.RequireHttpsMetadata = false;
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true,
		ValidIssuer = jwtSettings.Issuer,
		ValidAudience = jwtSettings.Audience,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
		ClockSkew = TimeSpan.Zero
	};

	// Configuración para SignalR: obtener token desde query string
	options.Events = new JwtBearerEvents
	{
		OnMessageReceived = context =>
		{
			var accessToken = context.Request.Query["access_token"];
			var path = context.HttpContext.Request.Path;

			// Si la petición es para el Hub de SignalR, obtener el token de la query
			if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
			{
				context.Token = accessToken;
			}

			return Task.CompletedTask;
		}
	};
});

builder.Services.AddAuthorization();

// Register Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IBidService, BidService>();

// Add Controllers
builder.Services.AddControllers();

// Configure SignalR
builder.Services.AddSignalR(options =>
{
	options.EnableDetailedErrors = builder.Environment.IsDevelopment();
	options.KeepAliveInterval = TimeSpan.FromSeconds(15);
	options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Configure Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
	options.EnableEndpointRateLimiting = true;
	options.StackBlockedRequests = false;
	options.HttpStatusCode = 429;
	options.RealIpHeader = "X-Real-IP";
	options.ClientIdHeader = "X-ClientId";
	options.GeneralRules = new List<RateLimitRule>
	{
		// Regla general: 100 requests por minuto
		new RateLimitRule
		{
			Endpoint = "*",
			Period = "1m",
			Limit = 100
		},
		// Regla específica para pujas: 10 pujas por minuto por usuario
		new RateLimitRule
		{
			Endpoint = "*:/api/auctions/*/bids",
			Period = "1m",
			Limit = 10
		},
		// Regla para login: 5 intentos por minuto
		new RateLimitRule
		{
			Endpoint = "*:/api/auth/login",
			Period = "1m",
			Limit = 5
		}
	};
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Configure OpenAPI/Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "BidUp API",
		Version = "v1",
		Description = "API para la aplicación de subastas BidUp.\n\nAutenticación: Bearer JWT.\n\nTiempo real: SignalR Hub en /hubs/auction (token vía query access_token)."
	});

	// Habilitar anotaciones (SwaggerOperation, etc.)
	options.EnableAnnotations();

	// Incluir comentarios XML para documentación enriquecida
	var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
	if (File.Exists(xmlPath))
	{
		options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
	}

	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "Bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Ingresa el token JWT en el formato: Bearer {token}"
	});

	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "Bearer"
				}
			},
			Array.Empty<string>()
		}
	});
});

// Configure CORS (SignalR requiere AllowCredentials, no compatible con AllowAnyOrigin)
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", policy =>
	{
		policy.WithOrigins(
				"http://localhost:3000",
				"http://localhost:5173",
				"https://localhost:3000",
				"https://localhost:5173")
			  .AllowAnyMethod()
			  .AllowAnyHeader()
			  .AllowCredentials();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "BidUp API v1");
		options.RoutePrefix = string.Empty;
	});
}

app.UseHttpsRedirection();

// Use Rate Limiting
app.UseIpRateLimiting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hubs
app.MapHub<AuctionHub>("/hubs/auction");

// Apply migrations automatically in development
if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	try
	{
		await context.Database.MigrateAsync();
		Console.WriteLine("✅ Database migrations applied successfully.");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"⚠️ Could not apply migrations: {ex.Message}");
		Console.WriteLine("📝 Make sure to run 'dotnet ef database update' to apply migrations.");
	}
}

app.Run();
