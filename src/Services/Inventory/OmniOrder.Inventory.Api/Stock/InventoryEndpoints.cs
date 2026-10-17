using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Inventory.Api.Stock;

public sealed record StoreDto(Guid Id, string Code, string Name, string City, string Address);

public sealed record StockLevelDto(string Sku, int OnHand, int Reserved, int Available);

public sealed record StoreAvailabilityDto(Guid StoreId, string Code, string Name, string City, IReadOnlyList<SkuAvailabilityDto> Items);

public sealed record SkuAvailabilityDto(string Sku, int Available);

public sealed record ReceiptRequest(int Quantity);

internal static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(string.Empty).WithTags("Inventory").WithOpenApi();

        api.MapGet("/stores", async (InventoryDbContext db, CancellationToken ct) =>
                await db.Stores.AsNoTracking().OrderBy(s => s.City)
                    .Select(s => new StoreDto(s.Id, s.Code, s.Name, s.City, s.Address))
                    .ToListAsync(ct))
            .WithSummary("Store network");

        api.MapGet("/stores/{storeId:guid}/stock", async (Guid storeId, InventoryDbContext db, CancellationToken ct) =>
                await db.StockLevels.AsNoTracking().Where(s => s.StoreId == storeId).OrderBy(s => s.Sku)
                    .Select(s => new StockLevelDto(s.Sku, s.OnHand, s.Reserved, s.OnHand - s.Reserved))
                    .ToListAsync(ct))
            .WithSummary("Stock of a store");

        api.MapGet("/availability", async ([FromQuery(Name = "sku")] string[] skus, InventoryDbContext db, CancellationToken ct) =>
            {
                var wanted = skus.Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
                var stores = await db.Stores.AsNoTracking().OrderBy(s => s.City).ToListAsync(ct);
                var levels = await db.StockLevels.AsNoTracking().Where(s => wanted.Contains(s.Sku)).ToListAsync(ct);
                return stores.Select(store => new StoreAvailabilityDto(
                    store.Id,
                    store.Code,
                    store.Name,
                    store.City,
                    wanted.Select(sku => new SkuAvailabilityDto(
                        sku,
                        levels.SingleOrDefault(l => l.StoreId == store.Id && l.Sku == sku)?.Available ?? 0)).ToList())).ToList();
            })
            .WithSummary("Availability of a cart in every store (store picker of the checkout)");

        api.MapPost("/stores/{storeId:guid}/stock/{sku}/receipts", async (Guid storeId, string sku, ReceiptRequest request, InventoryDbContext db, CancellationToken ct) =>
            {
                var normalized = sku.Trim().ToUpperInvariant();
                var level = await db.StockLevels.SingleOrDefaultAsync(s => s.StoreId == storeId && s.Sku == normalized, ct);
                if (level is null)
                {
                    if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct))
                    {
                        throw new NotFoundException("Store", storeId);
                    }

                    level = new StockLevel(storeId, normalized);
                    db.StockLevels.Add(level);
                }

                level.Receive(request.Quantity);
                await db.SaveChangesAsync(ct);
                return new StockLevelDto(level.Sku, level.OnHand, level.Reserved, level.Available);
            })
            .WithSummary("Goods receipt (replenishment)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
