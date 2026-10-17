using OmniOrder.Contracts;

namespace OmniOrder.Inventory.Api;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Click and collect: time for the store to prepare and for the customer to come.</summary>
    public TimeSpan ClickAndCollectHold { get; set; } = TimeSpan.FromDays(3);

    /// <summary>Reserve in store: the item is kept at the counter for a limited time.</summary>
    public TimeSpan ReserveInStoreHold { get; set; } = TimeSpan.FromDays(2);

    public TimeSpan ExpiryCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan HoldFor(FulfillmentMode mode) =>
        mode == FulfillmentMode.ClickAndCollect ? ClickAndCollectHold : ReserveInStoreHold;
}
