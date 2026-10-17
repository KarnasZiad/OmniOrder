using OmniOrder.ServiceDefaults;

namespace OmniOrder.Inventory.Api.Domain;

/// <summary>Stock of one SKU in one store. Invariant: 0 &lt;= reserved &lt;= on-hand.</summary>
public sealed class StockLevel
{
    private StockLevel()
    {
    }

    public StockLevel(Guid storeId, string sku, int onHand = 0)
    {
        if (onHand < 0)
        {
            throw new DomainException(InventoryErrors.InvalidQuantity, "Stock cannot be negative.");
        }

        Id = Guid.NewGuid();
        StoreId = storeId;
        Sku = sku;
        OnHand = onHand;
    }

    public Guid Id { get; private set; }

    public Guid StoreId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int Available => OnHand - Reserved;

    public uint Version { get; private set; }

    public void Receive(int quantity)
    {
        EnsurePositive(quantity);
        OnHand += quantity;
    }

    internal void Reserve(int quantity)
    {
        EnsurePositive(quantity);
        if (quantity > Available)
        {
            throw new DomainException(InventoryErrors.InsufficientStock, $"Only {Available} unit(s) of {Sku} available.");
        }

        Reserved += quantity;
    }

    internal void Release(int quantity)
    {
        EnsurePositive(quantity);
        Reserved = Math.Max(0, Reserved - quantity);
    }

    /// <summary>The reserved units physically leave the store (customer pickup).</summary>
    internal void Commit(int quantity)
    {
        EnsurePositive(quantity);
        if (quantity > Reserved || quantity > OnHand)
        {
            throw new DomainException(InventoryErrors.InvalidQuantity, $"Cannot commit {quantity} unit(s) of {Sku}: only {Reserved} reserved.");
        }

        Reserved -= quantity;
        OnHand -= quantity;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException(InventoryErrors.InvalidQuantity, "Quantity must be strictly positive.");
        }
    }
}
