namespace OmniOrder.Inventory.Api.Domain;

public static class InventoryErrors
{
    public const string InvalidQuantity = "inventory.invalid_quantity";
    public const string InsufficientStock = "inventory.insufficient_stock";
    public const string ReservationClosed = "inventory.reservation_closed";
}
