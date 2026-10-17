using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Catalog;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Ordering.Api.Orders;

public sealed class OrderService(OrderingDbContext db, ICatalogClient catalog, IPublishEndpoint publisher, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>Checkout: prices the cart with the catalogue, stores the order and asks Inventory to reserve the stock.</summary>
    public async Task<OrderDto> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var requested = request.Lines.Select(l => (Sku: l.Sku.Trim().ToUpperInvariant(), l.Quantity)).ToList();
        var products = (await catalog.LookupAsync(requested.Select(l => l.Sku).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.Sku);

        var unknown = requested.Select(l => l.Sku).Where(sku => !products.ContainsKey(sku)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            throw new DomainException(OrderErrors.UnknownProducts, $"Unknown products: {string.Join(", ", unknown)}.");
        }

        var lines = requested
            .Select(l => new PricedLine(l.Sku, products[l.Sku].Name, products[l.Sku].Price, l.Quantity))
            .ToList();

        var order = Order.Place(request.StoreId, request.Mode, request.CustomerName, request.CustomerEmail, lines, Now);
        db.Orders.Add(order);

        // Transactional outbox: the order and the OrderSubmitted message are committed together.
        await publisher.Publish(
            new OrderSubmitted(
                order.Id,
                order.Number,
                order.StoreId,
                order.Mode,
                order.Items.Select(i => new OrderLineContract(i.Sku, i.Quantity)).ToList(),
                order.CreatedAt),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.From(order);
    }

    public async Task<OrderDto> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        OrderDto.From(await LoadAsync(orderId, cancellationToken));

    public async Task<IReadOnlyList<OrderDto>> SearchAsync(Guid? storeId, string? customerEmail, OrderStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();
        if (storeId is not null)
        {
            query = query.Where(o => o.StoreId == storeId);
        }

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            var email = customerEmail.Trim().ToLowerInvariant();
            query = query.Where(o => o.CustomerEmail == email);
        }

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return orders.Select(OrderDto.From).ToList();
    }

    public async Task<OrderDto> MarkReadyAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);
        order.MarkReadyForPickup(Now);
        await publisher.Publish(new OrderReadyForPickup(order.Id, order.Number, order.StoreId, order.CustomerEmail, Now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

    public async Task<OrderDto> CollectAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);
        order.Collect(Now);
        await publisher.Publish(new OrderCollected(order.Id, order.StoreId, Now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, string? reason, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);
        order.Cancel(reason ?? "Cancelled", Now);
        await publisher.Publish(new OrderCancelled(order.Id, order.StoreId, order.StatusReason ?? "Cancelled", Now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

    private async Task<Order> LoadAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
        ?? throw new NotFoundException("Order", orderId);
}
