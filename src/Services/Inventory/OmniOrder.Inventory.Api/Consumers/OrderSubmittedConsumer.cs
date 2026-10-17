using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Consumers;

/// <summary>Reserves the whole order in the pickup store, or rejects it.</summary>
public sealed partial class OrderSubmittedConsumer(
    InventoryDbContext db,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<OrderSubmittedConsumer> logger) : IConsumer<OrderSubmitted>
{
    // The .NET 8 [LoggerMessage] generator needs an ILogger field (primary constructor parameters are not supported).
    private readonly ILogger<OrderSubmittedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var order = context.Message;
        var ct = context.CancellationToken;

        // Idempotency (on top of the inbox): a redelivered order gets the same answer.
        var existing = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.OrderId == order.OrderId, ct);
        if (existing is not null)
        {
            if (existing.Status == ReservationStatus.Active)
            {
                await context.Publish(new StockReserved(order.OrderId, order.StoreId, existing.ExpiresAt));
            }

            return;
        }

        if (!await db.Stores.AnyAsync(s => s.Id == order.StoreId, ct))
        {
            await context.Publish(new StockReservationFailed(order.OrderId, order.StoreId, "Unknown store", []));
            return;
        }

        var levels = await db.StockLevelsAsync(order.StoreId, order.Lines.Select(l => l.Sku), ct);
        var result = StockAllocator.TryReserve(order, levels, clock.GetUtcNow(), options.Value.HoldFor(order.Mode));

        if (result.Succeeded)
        {
            db.Reservations.Add(result.Reservation!);
            await context.Publish(new StockReserved(order.OrderId, order.StoreId, result.Reservation!.ExpiresAt));
            LogReserved(order.OrderNumber, order.StoreId);
        }
        else
        {
            await context.Publish(new StockReservationFailed(order.OrderId, order.StoreId, "Insufficient stock in the selected store", result.UnavailableSkus));
            LogRejected(order.OrderNumber, string.Join(", ", result.UnavailableSkus));
        }

        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} reserved in store {StoreId}")]
    private partial void LogReserved(string orderNumber, Guid storeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} rejected, unavailable: {Skus}")]
    private partial void LogRejected(string orderNumber, string skus);
}
