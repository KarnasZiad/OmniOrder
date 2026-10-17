using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Domain;

namespace OmniOrder.Ordering.Api.Orders;

public sealed record OrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderRequest(
    Guid StoreId,
    FulfillmentMode Mode,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<OrderLineRequest> Lines);

public sealed record CancelOrderRequest(string? Reason);

public sealed record OrderItemDto(string Sku, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string Number,
    Guid StoreId,
    FulfillmentMode Mode,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    PaymentStatus Payment,
    decimal Total,
    string? StatusReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PickupDeadline,
    IReadOnlyList<OrderItemDto> Items)
{
    public static OrderDto From(Order o) => new(
        o.Id,
        o.Number,
        o.StoreId,
        o.Mode,
        o.CustomerName,
        o.CustomerEmail,
        o.Status,
        o.Payment,
        o.Total,
        o.StatusReason,
        o.CreatedAt,
        o.UpdatedAt,
        o.PickupDeadline,
        o.Items.Select(i => new OrderItemDto(i.Sku, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());
}
