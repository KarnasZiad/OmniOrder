namespace OmniOrder.Ordering.Api.Domain;

public enum OrderStatus
{
    /// <summary>Submitted, waiting for the store to reserve the stock.</summary>
    Pending,

    /// <summary>Click and collect: stock reserved and payment captured, the store has to prepare the order.</summary>
    Confirmed,

    /// <summary>Reserve in store: stock reserved, the customer pays at the counter.</summary>
    Reserved,

    /// <summary>Prepared by the store staff, waiting for the customer.</summary>
    ReadyForPickup,

    Collected,
    Rejected,
    Cancelled,
    Expired,
}

public enum PaymentStatus
{
    /// <summary>Click and collect: paid online at checkout (simulated payment).</summary>
    Captured,

    Refunded,

    /// <summary>Reserve in store: to be paid at the counter.</summary>
    DueInStore,

    PaidInStore,

    /// <summary>Reservation cancelled or expired before any payment.</summary>
    None,
}
