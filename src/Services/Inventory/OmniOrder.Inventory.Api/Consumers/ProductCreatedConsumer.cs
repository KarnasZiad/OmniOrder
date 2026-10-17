using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Consumers;

/// <summary>A new product is referenced in every store, with an empty stock.</summary>
public sealed class ProductCreatedConsumer(InventoryDbContext db) : IConsumer<ProductCreated>
{
    public async Task Consume(ConsumeContext<ProductCreated> context)
    {
        var ct = context.CancellationToken;
        var sku = context.Message.Sku;
        var storesWithLine = await db.StockLevels.Where(s => s.Sku == sku).Select(s => s.StoreId).ToListAsync(ct);
        var stores = await db.Stores.Where(s => !storesWithLine.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);

        db.StockLevels.AddRange(stores.Select(storeId => new StockLevel(storeId, sku)));
        await db.SaveChangesAsync(ct);
    }
}
