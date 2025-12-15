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
	}
}
