using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using uManageIt.Website.Domain;

namespace uManageIt.Website.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ManagedWebsite> Websites => Set<ManagedWebsite>();
    public DbSet<WebsiteApiKey> WebsiteApiKeys => Set<WebsiteApiKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ManagedWebsite>(entity =>
        {
            entity.ToTable("managed_websites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Owner)
                .WithMany(x => x.Websites)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WebsiteApiKey>(entity =>
        {
            entity.ToTable("website_api_keys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KeyHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.KeyHash).IsUnique();
            entity.HasOne(x => x.Website)
                .WithMany(x => x.ApiKeys)
                .HasForeignKey(x => x.WebsiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
