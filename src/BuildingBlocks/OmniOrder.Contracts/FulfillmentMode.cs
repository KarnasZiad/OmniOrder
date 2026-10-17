namespace OmniOrder.Contracts;

/// <summary>How the customer gets the goods.</summary>
public enum FulfillmentMode
{
    /// <summary>Paid online, prepared by the store, picked up by the customer.</summary>
    ClickAndCollect,

    /// <summary>Reserved online for a limited time, paid at the store counter.</summary>
    ReserveInStore,
}
