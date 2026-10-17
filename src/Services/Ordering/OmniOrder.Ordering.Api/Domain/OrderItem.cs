namespace OmniOrder.Ordering.Api.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    internal OrderItem(string sku, string productName, decimal unitPrice, int quantity)
    {
        Sku = sku;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public int Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
