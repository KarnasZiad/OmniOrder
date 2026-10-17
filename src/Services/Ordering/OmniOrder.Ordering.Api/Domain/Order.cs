using OmniOrder.Contracts;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Ordering.Api.Domain;

public sealed record PricedLine(string Sku, string ProductName, decimal UnitPrice, int Quantity);

/// <summary>
/// Omnichannel order. State machine:
/// <code>
/// Pending ──StockReserved──► Confirmed (click and collect) ─┐
///    │                     └► Reserved  (reserve in store) ─┴─ready──► ReadyForPickup ──collect──► Collected
///    └──StockReservationFailed──► Rejected
/// Pending / Confirmed / Reserved / ReadyForPickup ──cancel──► Cancelled
/// Confirmed / Reserved / ReadyForPickup ──reservation expired──► Expired
/// </code>
/// </summary>
public sealed class Order
{
    public const int MaxLines = 20;
    public const int MaxQuantityPerLine = 10;

    private static readonly OrderStatus[] Active = [OrderStatus.Confirmed, OrderStatus.Reserved, OrderStatus.ReadyForPickup];

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid StoreId { get; private set; }

    public FulfillmentMode Mode { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public PaymentStatus Payment { get; private set; }

    public decimal Total { get; private set; }

    public string? StatusReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PickupDeadline { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public bool IsFinal => Status is OrderStatus.Collected or OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired;

    public static Order Place(
        Guid storeId,
        FulfillmentMode mode,
        string customerName,
        string customerEmail,
        IReadOnlyCollection<PricedLine> lines,
        DateTimeOffset now)
    {
        if (storeId == Guid.Empty)
        {
            throw new DomainException(OrderErrors.StoreRequired, "A pickup store must be selected.");
        }

        if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerEmail) || !customerEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException(OrderErrors.CustomerRequired, "Customer name and a valid email are required.");
        }

        if (lines.Count == 0 || lines.Count > MaxLines)
        {
            throw new DomainException(OrderErrors.InvalidLines, $"An order contains between 1 and {MaxLines} lines.");
        }

        if (lines.Any(l => l.Quantity is <= 0 or > MaxQuantityPerLine))
        {
            throw new DomainException(OrderErrors.InvalidLines, $"Quantities must be between 1 and {MaxQuantityPerLine}.");
        }

        if (lines.GroupBy(l => l.Sku).Any(g => g.Count() > 1))
        {
            throw new DomainException(OrderErrors.InvalidLines, "Each product can only appear once in an order.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Mode = mode,
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
            Status = OrderStatus.Pending,
            // Click and collect is paid online at checkout (payment is simulated in this demo).
            Payment = mode == FulfillmentMode.ClickAndCollect ? PaymentStatus.Captured : PaymentStatus.DueInStore,
            CreatedAt = now,
            UpdatedAt = now,
        };
        order.Number = $"OO-{now:yyMMdd}-{order.Id.ToString("N")[..6].ToUpperInvariant()}";
        order._items.AddRange(lines.Select(l => new OrderItem(l.Sku, l.ProductName, l.UnitPrice, l.Quantity)));
        order.Total = order._items.Sum(i => i.LineTotal);
        return order;
    }

    /// <summary>Inventory reserved every line in the pickup store.</summary>
    public void ConfirmStock(DateTimeOffset pickupDeadline, DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Pending);
        Status = Mode == FulfillmentMode.ClickAndCollect ? OrderStatus.Confirmed : OrderStatus.Reserved;
        PickupDeadline = pickupDeadline;
        Touch(now, null);
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Rejected;
        Refund();
        Touch(now, reason);
    }

    /// <summary>The store staff picked the items and put the order aside.</summary>
    public void MarkReadyForPickup(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Confirmed, OrderStatus.Reserved);
        Status = OrderStatus.ReadyForPickup;
        Touch(now, null);
    }

    public void Collect(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.ReadyForPickup);
        Status = OrderStatus.Collected;
        if (Payment == PaymentStatus.DueInStore)
        {
            Payment = PaymentStatus.PaidInStore;
        }

        Touch(now, null);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Reserved, OrderStatus.ReadyForPickup);
        Status = OrderStatus.Cancelled;
        Refund();
        Touch(now, string.IsNullOrWhiteSpace(reason) ? "Cancelled" : reason.Trim());
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureStatus(Active);
        Status = OrderStatus.Expired;
        Refund();
        Touch(now, "Not collected before the pickup deadline");
    }

    private void Refund() =>
        Payment = Payment switch
        {
            PaymentStatus.Captured => PaymentStatus.Refunded,
            PaymentStatus.DueInStore => PaymentStatus.None,
            _ => Payment,
        };

    private void EnsureStatus(params OrderStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(OrderErrors.InvalidTransition, $"Order {Number} is {Status}: operation not allowed.");
        }
    }

    private void Touch(DateTimeOffset now, string? reason)
    {
        UpdatedAt = now;
        StatusReason = reason;
    }
}

public static class OrderErrors
{
    public const string StoreRequired = "order.store_required";
    public const string CustomerRequired = "order.customer_required";
    public const string InvalidLines = "order.invalid_lines";
    public const string UnknownProducts = "order.unknown_products";
    public const string InvalidTransition = "order.invalid_transition";
}
