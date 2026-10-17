namespace OmniOrder.Contracts;

public sealed record OrderLineContract(string Sku, int Quantity);

/// <summary>Published by Ordering when a customer checks out. Inventory must reserve every line (all or nothing).</summary>
public sealed record OrderSubmitted(
    Guid OrderId,
    string OrderNumber,
    Guid StoreId,
    FulfillmentMode Mode,
    IReadOnlyList<OrderLineContract> Lines,
    DateTimeOffset SubmittedAt);

/// <summary>The store staff prepared the order: the customer can come and pick it up.</summary>
public sealed record OrderReadyForPickup(Guid OrderId, string OrderNumber, Guid StoreId, string CustomerEmail, DateTimeOffset ReadyAt);

/// <summary>The customer picked the order up: the reserved stock leaves the store.</summary>
public sealed record OrderCollected(Guid OrderId, Guid StoreId, DateTimeOffset CollectedAt);

/// <summary>The order was cancelled: any reservation must be released.</summary>
public sealed record OrderCancelled(Guid OrderId, Guid StoreId, string Reason, DateTimeOffset CancelledAt);
