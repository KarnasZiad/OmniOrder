using Microsoft.EntityFrameworkCore;
using OmniOrder.Inventory.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Inventory.Api.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();

    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    /// <summary>Loads the stock levels of a store for the given SKUs, keyed by SKU.</summary>
    public async Task<Dictionary<string, StockLevel>> StockLevelsAsync(Guid storeId, IEnumerable<string> skus, CancellationToken cancellationToken)
    {
        var wanted = skus.Distinct().ToList();
        return await StockLevels.Where(s => s.StoreId == storeId && wanted.Contains(s.Sku)).ToDictionaryAsync(s => s.Sku, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(store =>
        {
            store.ToTable("stores");
            store.HasKey(s => s.Id);
            store.Property(s => s.Id).ValueGeneratedNever();
            store.HasIndex(s => s.Code).IsUnique();
            store.Property(s => s.Code).HasMaxLength(20);
            store.Property(s => s.Name).HasMaxLength(100);
            store.Property(s => s.City).HasMaxLength(100);
            store.Property(s => s.Address).HasMaxLength(200);
        });

        modelBuilder.Entity<StockLevel>(level =>
        {
            level.ToTable("stock_levels");
            level.HasKey(s => s.Id);
            level.Property(s => s.Id).ValueGeneratedNever();
            level.HasIndex(s => new { s.StoreId, s.Sku }).IsUnique();
            level.Property(s => s.Sku).HasMaxLength(20);
            level.HasOne<Store>().WithMany().HasForeignKey(s => s.StoreId);
            level.Ignore(s => s.Available);
            if (Database.IsNpgsql())
            {
                // Optimistic concurrency on PostgreSQL xmin: two orders cannot book the same last unit.
                level.Property(s => s.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
                level.ToTable(t => t.HasCheckConstraint("ck_stock_levels_quantities", "reserved >= 0 AND reserved <= on_hand"));
            }
            else
            {
                level.Ignore(s => s.Version);
            }
        });

        modelBuilder.Entity<Reservation>(reservation =>
        {
            reservation.ToTable("reservations");
            reservation.HasKey(r => r.Id);
            reservation.Property(r => r.Id).ValueGeneratedNever();
            reservation.HasIndex(r => r.OrderId).IsUnique();
            reservation.HasIndex(r => new { r.Status, r.ExpiresAt });
            reservation.Property(r => r.OrderNumber).HasMaxLength(30);
            reservation.Property(r => r.Mode).HasConversion<string>().HasMaxLength(30);
            reservation.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            reservation.OwnsMany(r => r.Lines, line =>
            {
                line.ToTable("reservation_lines");
                line.WithOwner().HasForeignKey("ReservationId");
                line.HasKey(l => l.Id);
                line.Property(l => l.Sku).HasMaxLength(20);
            });
            reservation.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.AddMessagingTables();
    }
}
