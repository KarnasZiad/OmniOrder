using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Domain;

namespace OmniOrder.Ordering.Api.Consumers;

public sealed partial class StockReservedConsumer(OrderingDbContext db, TimeProvider clock, ILogger<StockReservedConsumer> logger)
    : IConsumer<StockReserved>
{
    public async Task Consume(ConsumeContext<StockReserved> context)
    {
        var message = context.Message;
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken);
        if (order is null)
        {
            return;
        }

        switch (order.Status)
        {
            case OrderStatus.Pending:
                order.ConfirmStock(message.ExpiresAt, clock.GetUtcNow());
                LogConfirmed(order.Number, order.Status);
                break;

            case OrderStatus.Cancelled:
                // The customer cancelled before the reservation arrived: compensate.
                await context.Publish(new OrderCancelled(order.Id, order.StoreId, "Cancelled before reservation", clock.GetUtcNow()));
                break;

            default:
                // Duplicate or late message: nothing to do.
                return;
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} stock reserved, now {Status}")]
    private partial void LogConfirmed(string orderNumber, OrderStatus status);
}
