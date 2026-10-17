using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Consumers;

/// <summary>Gives the reserved units back to the available stock.</summary>
public sealed class OrderCancelledConsumer(InventoryDbContext db, TimeProvider clock) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var ct = context.CancellationToken;
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.OrderId == context.Message.OrderId, ct);
        if (reservation is not { Status: ReservationStatus.Active })
        {
            return;
        }

        var levels = await db.StockLevelsAsync(reservation.StoreId, reservation.Lines.Select(l => l.Sku), ct);
        reservation.Release(levels, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
