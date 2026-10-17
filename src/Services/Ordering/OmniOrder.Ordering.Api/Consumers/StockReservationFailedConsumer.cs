using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Domain;

namespace OmniOrder.Ordering.Api.Consumers;

public sealed class StockReservationFailedConsumer(OrderingDbContext db, TimeProvider clock) : IConsumer<StockReservationFailed>
{
    public async Task Consume(ConsumeContext<StockReservationFailed> context)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == context.Message.OrderId, context.CancellationToken);
        if (order is not { Status: OrderStatus.Pending })
        {
            return;
        }

        var skus = context.Message.UnavailableSkus.Count > 0 ? $" ({string.Join(", ", context.Message.UnavailableSkus)})" : string.Empty;
        order.Reject($"{context.Message.Reason}{skus}", clock.GetUtcNow());
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
