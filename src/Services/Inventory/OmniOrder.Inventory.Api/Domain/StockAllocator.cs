using OmniOrder.Contracts;

namespace OmniOrder.Inventory.Api.Domain;

public sealed record AllocationResult(Reservation? Reservation, IReadOnlyList<string> UnavailableSkus)
{
    public bool Succeeded => Reservation is not null;
}

/// <summary>Reserves every line of an order in one store, or nothing at all.</summary>
public static class StockAllocator
{
    public static AllocationResult TryReserve(
        OrderSubmitted order,
        IReadOnlyDictionary<string, StockLevel> levels,
        DateTimeOffset now,
        TimeSpan holdFor)
    {
        // Same SKU on several lines: check the total requested quantity.
        var requested = order.Lines
            .GroupBy(l => l.Sku)
            .Select(g => (Sku: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .ToList();

        var unavailable = requested
            .Where(l => !levels.TryGetValue(l.Sku, out var level) || level.Available < l.Quantity)
            .Select(l => l.Sku)
            .ToList();

        if (unavailable.Count > 0)
        {
            return new AllocationResult(null, unavailable);
        }

        foreach (var (sku, quantity) in requested)
        {
            levels[sku].Reserve(quantity);
        }

        var reservation = Reservation.Create(order.OrderId, order.OrderNumber, order.StoreId, order.Mode, requested, now, now + holdFor);
        return new AllocationResult(reservation, []);
    }
}
