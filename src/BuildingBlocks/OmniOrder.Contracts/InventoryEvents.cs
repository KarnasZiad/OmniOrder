namespace OmniOrder.Contracts;

/// <summary>Every line of the order is set aside in the store until <see cref="ExpiresAt"/>.</summary>
public sealed record StockReserved(Guid OrderId, Guid StoreId, DateTimeOffset ExpiresAt);

/// <summary>At least one line could not be reserved: nothing was reserved.</summary>
public sealed record StockReservationFailed(Guid OrderId, Guid StoreId, string Reason, IReadOnlyList<string> UnavailableSkus);

/// <summary>The customer did not pick the order up in time: the reservation was released.</summary>
public sealed record StockReservationExpired(Guid OrderId, Guid StoreId, DateTimeOffset ExpiredAt);
