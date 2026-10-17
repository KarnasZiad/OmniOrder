using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Consumers;

/// <summary>Customer pickup: the reserved units leave the store stock.</summary>
public sealed class OrderCollectedConsumer(InventoryDbContext db, TimeProvider clock) : IConsumer<OrderCollected>
{
    public async Task Consume(ConsumeContext<OrderCollected> context)
    {
        var ct = context.CancellationToken;
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.OrderId == context.Message.OrderId, ct);
        if (reservation is not { Status: ReservationStatus.Active })
        {
            return;
        }

        var levels = await db.StockLevelsAsync(reservation.StoreId, reservation.Lines.Select(l => l.Sku), ct);
        reservation.Commit(levels, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
