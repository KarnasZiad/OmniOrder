using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Domain;

namespace OmniOrder.Ordering.Api.Consumers;

public sealed class StockReservationExpiredConsumer(OrderingDbContext db, TimeProvider clock) : IConsumer<StockReservationExpired>
{
    public async Task Consume(ConsumeContext<StockReservationExpired> context)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == context.Message.OrderId, context.CancellationToken);
        if (order is not { Status: OrderStatus.Confirmed or OrderStatus.Reserved or OrderStatus.ReadyForPickup })
        {
            return;
        }

        order.Expire(clock.GetUtcNow());
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
