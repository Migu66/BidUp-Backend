# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["BidUp.Api/BidUp.Api.csproj", "BidUp.Api/"]

# Restore dependencies
RUN dotnet restore "BidUp.Api/BidUp.Api.csproj"

# Copy source code
COPY . .

# Build the application
RUN dotnet build "BidUp.Api/BidUp.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "BidUp.Api/BidUp.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published files
COPY --from=publish /app/publish .

# Create a non-root user
RUN useradd -m -u 1001 appuser && chown -R appuser:appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
	CMD wget --no-verbose --tries=1 --spider http://localhost/weatherforecast || exit 1

# Entry point
ENTRYPOINT ["dotnet", "BidUp.Api.dll"]
