using Microsoft.EntityFrameworkCore;
using OmniOrder.Ordering.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Ordering.Api.Data;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever();
            order.HasIndex(o => o.Number).IsUnique();
            order.HasIndex(o => new { o.StoreId, o.Status });
            order.HasIndex(o => o.CustomerEmail);
            order.Property(o => o.Number).HasMaxLength(30);
            order.Property(o => o.CustomerName).HasMaxLength(120);
            order.Property(o => o.CustomerEmail).HasMaxLength(200);
            order.Property(o => o.StatusReason).HasMaxLength(300);
            order.Property(o => o.Mode).HasConversion<string>().HasMaxLength(30);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            order.Property(o => o.Payment).HasConversion<string>().HasMaxLength(30);
            order.Property(o => o.Total).HasPrecision(12, 2);
            order.Ignore(o => o.IsFinal);

            if (Database.IsNpgsql())
            {
                order.Property(o => o.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
            }
            else
            {
                order.Ignore(o => o.Version);
            }

            order.OwnsMany(o => o.Items, item =>
            {
                item.ToTable("order_items");
                item.WithOwner().HasForeignKey("OrderId");
                item.HasKey(i => i.Id);
                item.Property(i => i.Sku).HasMaxLength(20);
                item.Property(i => i.ProductName).HasMaxLength(150);
                item.Property(i => i.UnitPrice).HasPrecision(10, 2);
                item.Ignore(i => i.LineTotal);
            });
            order.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.AddMessagingTables();
    }
}
