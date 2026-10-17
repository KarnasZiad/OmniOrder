using Microsoft.EntityFrameworkCore;
using OmniOrder.Catalog.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Catalog.Api.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("products");
            product.HasKey(p => p.Id);
            product.HasIndex(p => p.Sku).IsUnique();
            product.Property(p => p.Sku).HasMaxLength(20);
            product.Property(p => p.Name).HasMaxLength(150);
            product.Property(p => p.Description).HasMaxLength(1000);
            product.Property(p => p.Category).HasMaxLength(60);
            product.Property(p => p.Price).HasPrecision(10, 2);
        });

        modelBuilder.AddMessagingTables();
    }
}
