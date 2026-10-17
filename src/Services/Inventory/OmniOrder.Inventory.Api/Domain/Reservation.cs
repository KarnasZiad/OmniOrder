using OmniOrder.Contracts;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Inventory.Api.Domain;

public enum ReservationStatus
{
    Active,
    Committed,
    Released,
    Expired,
}

public sealed class ReservationLine
{
    private ReservationLine()
    {
    }

    internal ReservationLine(string sku, int quantity)
    {
        Sku = sku;
        Quantity = quantity;
    }

    public int Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int Quantity { get; private set; }
}

/// <summary>Stock set aside in a store for one order.</summary>
public sealed class Reservation
{
    private readonly List<ReservationLine> _lines = [];

    private Reservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid StoreId { get; private set; }

    public FulfillmentMode Mode { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<ReservationLine> Lines => _lines;

    internal static Reservation Create(
        Guid orderId, string orderNumber, Guid storeId, FulfillmentMode mode, IEnumerable<(string Sku, int Quantity)> lines, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            OrderNumber = orderNumber,
            StoreId = storeId,
            Mode = mode,
            Status = ReservationStatus.Active,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        reservation._lines.AddRange(lines.Select(l => new ReservationLine(l.Sku, l.Quantity)));
        return reservation;
    }

    public bool IsDue(DateTimeOffset now) => Status == ReservationStatus.Active && now >= ExpiresAt;

    /// <summary>Customer pickup: the reserved units leave the store.</summary>
    public void Commit(IReadOnlyDictionary<string, StockLevel> levels, DateTimeOffset now)
    {
        EnsureActive();
        foreach (var line in _lines)
        {
            Level(levels, line.Sku).Commit(line.Quantity);
        }

        Close(ReservationStatus.Committed, now);
    }

    public void Release(IReadOnlyDictionary<string, StockLevel> levels, DateTimeOffset now) => GiveBack(levels, ReservationStatus.Released, now);

    public void Expire(IReadOnlyDictionary<string, StockLevel> levels, DateTimeOffset now) => GiveBack(levels, ReservationStatus.Expired, now);

    private void GiveBack(IReadOnlyDictionary<string, StockLevel> levels, ReservationStatus status, DateTimeOffset now)
    {
        EnsureActive();
        foreach (var line in _lines)
        {
            Level(levels, line.Sku).Release(line.Quantity);
        }

        Close(status, now);
    }

    private void EnsureActive()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException(InventoryErrors.ReservationClosed, $"Reservation of order {OrderNumber} is already {Status}.");
        }
    }

    private static StockLevel Level(IReadOnlyDictionary<string, StockLevel> levels, string sku) =>
        levels.TryGetValue(sku, out var level)
            ? level
            : throw new InvalidOperationException($"Stock level for {sku} was not loaded.");

    private void Close(ReservationStatus status, DateTimeOffset now)
    {
        Status = status;
        ClosedAt = now;
    }
}
