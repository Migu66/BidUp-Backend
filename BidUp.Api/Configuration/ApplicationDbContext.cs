using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BidUp.Api.Domain.Entities;

namespace BidUp.Api.Configuration;

public class ApplicationDbContext : IdentityDbContext<User, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>
{
	public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
	{
	}

	public DbSet<RefreshToken> RefreshTokens { get; set; }
	public DbSet<Category> Categories { get; set; }
	public DbSet<Auction> Auctions { get; set; }
	public DbSet<Bid> Bids { get; set; }

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		// Configuración de User
		builder.Entity<User>(entity =>
		{
			entity.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
			entity.Property(u => u.LastName).HasMaxLength(50).IsRequired();
			entity.Property(u => u.CreatedAt).IsRequired();
			entity.Property(u => u.IsActive).HasDefaultValue(true);
		});

		// Configuración de RefreshToken
		builder.Entity<RefreshToken>(entity =>
		{
			entity.HasKey(rt => rt.Id);
			entity.Property(rt => rt.Token).HasMaxLength(500).IsRequired();
			entity.Property(rt => rt.ExpiresAt).IsRequired();
			entity.Property(rt => rt.CreatedAt).IsRequired();

			entity.HasOne(rt => rt.User)
				  .WithMany()
				  .HasForeignKey(rt => rt.UserId)
				  .OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(rt => rt.Token).IsUnique();
		});

		// Renombrar tablas de Identity para mantener consistencia
		builder.Entity<User>().ToTable("Users");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>().ToTable("Roles");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");
		builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

		// Configuración de Category
		builder.Entity<Category>(entity =>
		{
			entity.HasKey(c => c.Id);
			entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
			entity.Property(c => c.Description).HasMaxLength(500);
			entity.Property(c => c.ImageUrl).HasMaxLength(500);
			entity.HasIndex(c => c.Name).IsUnique();
		});

		// Configuración de Auction
		builder.Entity<Auction>(entity =>
		{
			entity.HasKey(a => a.Id);
			entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
			entity.Property(a => a.Description).HasMaxLength(2000).IsRequired();
			entity.Property(a => a.ImageUrl).HasMaxLength(500);
			entity.Property(a => a.StartingPrice).HasPrecision(18, 2).IsRequired();
			entity.Property(a => a.CurrentPrice).HasPrecision(18, 2).IsRequired();
			entity.Property(a => a.ReservePrice).HasPrecision(18, 2);
			entity.Property(a => a.MinBidIncrement).HasPrecision(18, 2).HasDefaultValue(1.00m);
			entity.Property(a => a.Status).HasConversion<int>();

			// Relación con Seller (User)
			entity.HasOne(a => a.Seller)
				  .WithMany()
				  .HasForeignKey(a => a.SellerId)
				  .OnDelete(DeleteBehavior.Restrict);

			// Relación con Category
			entity.HasOne(a => a.Category)
				  .WithMany(c => c.Auctions)
				  .HasForeignKey(a => a.CategoryId)
				  .OnDelete(DeleteBehavior.Restrict);

			// Relación con WinnerBid
			entity.HasOne(a => a.WinnerBid)
				  .WithMany()
				  .HasForeignKey(a => a.WinnerBidId)
				  .OnDelete(DeleteBehavior.SetNull);

			// Índices para búsquedas optimizadas
			entity.HasIndex(a => a.Status);
			entity.HasIndex(a => a.EndTime);
			entity.HasIndex(a => new { a.Status, a.EndTime });
		});

		// Configuración de Bid
		builder.Entity<Bid>(entity =>
		{
			entity.HasKey(b => b.Id);
			entity.Property(b => b.Amount).HasPrecision(18, 2).IsRequired();
			entity.Property(b => b.Timestamp).IsRequired();
			entity.Property(b => b.IpAddress).HasMaxLength(45); // IPv6 max length

			// Relación con Auction
			entity.HasOne(b => b.Auction)
				  .WithMany(a => a.Bids)
				  .HasForeignKey(b => b.AuctionId)
				  .OnDelete(DeleteBehavior.Cascade);

			// Relación con Bidder (User)
			entity.HasOne(b => b.Bidder)
				  .WithMany()
				  .HasForeignKey(b => b.BidderId)
				  .OnDelete(DeleteBehavior.Restrict);

			// Índice compuesto para resolver conflictos de pujas simultáneas
			// Ordenamos por Timestamp para determinar qué puja llegó primero
			entity.HasIndex(b => new { b.AuctionId, b.Timestamp });
			entity.HasIndex(b => new { b.AuctionId, b.Amount });
		});
	}
}
