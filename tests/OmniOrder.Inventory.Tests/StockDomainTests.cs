using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Inventory.Tests;

public sealed class StockDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Store = Guid.NewGuid();

    private static OrderSubmitted Order(params (string Sku, int Quantity)[] lines) =>
        new(Guid.NewGuid(), "OO-TEST", Store, FulfillmentMode.ClickAndCollect, lines.Select(l => new OrderLineContract(l.Sku, l.Quantity)).ToList(), Now);

    private static Dictionary<string, StockLevel> Levels(params (string Sku, int OnHand)[] levels) =>
        levels.ToDictionary(l => l.Sku, l => new StockLevel(Store, l.Sku, l.OnHand));

    [Test]
    public void Stock_cannot_be_created_negative() =>
        Assert.That(() => new StockLevel(Store, "A", -1), Throws.InstanceOf<DomainException>());

    [TestCase(0)]
    [TestCase(-2)]
    public void Receipt_quantity_must_be_positive(int quantity) =>
        Assert.That(() => new StockLevel(Store, "A", 5).Receive(quantity), Throws.InstanceOf<DomainException>());

    [Test]
    public void Allocation_reserves_every_line_when_the_store_has_the_stock()
    {
        var levels = Levels(("A", 5), ("B", 2));

        var result = StockAllocator.TryReserve(Order(("A", 3), ("B", 2)), levels, Now, TimeSpan.FromHours(48));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Reservation!.ExpiresAt, Is.EqualTo(Now.AddHours(48)));
        Assert.That(levels["A"].Available, Is.EqualTo(2));
        Assert.That(levels["B"].Available, Is.EqualTo(0));
        Assert.That(result.Reservation.Lines.Select(l => (l.Sku, l.Quantity)), Is.EquivalentTo(new[] { ("A", 3), ("B", 2) }));
    }

    [Test]
    public void Allocation_is_all_or_nothing()
    {
        var levels = Levels(("A", 5), ("B", 1));

        var result = StockAllocator.TryReserve(Order(("A", 3), ("B", 2), ("C", 1)), levels, Now, TimeSpan.FromHours(48));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.UnavailableSkus, Is.EquivalentTo(new[] { "B", "C" }));
        Assert.That(levels["A"].Reserved, Is.Zero, "nothing must be reserved when one line is missing");
        Assert.That(levels["B"].Reserved, Is.Zero);
    }

    [Test]
    public void Allocation_sums_duplicate_lines_of_the_same_sku()
    {
        var levels = Levels(("A", 3));

        var result = StockAllocator.TryReserve(Order(("A", 2), ("A", 2)), levels, Now, TimeSpan.FromHours(1));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(levels["A"].Reserved, Is.Zero);
    }

    [Test]
    public void Committing_a_reservation_removes_the_units_from_the_store()
    {
        var levels = Levels(("A", 5));
        var reservation = StockAllocator.TryReserve(Order(("A", 2)), levels, Now, TimeSpan.FromHours(1)).Reservation!;

        reservation.Commit(levels, Now);

        Assert.That(reservation.Status, Is.EqualTo(ReservationStatus.Committed));
        Assert.That((levels["A"].OnHand, levels["A"].Reserved), Is.EqualTo((3, 0)));
    }

    [Test]
    public void Releasing_or_expiring_a_reservation_gives_the_units_back()
    {
        var levels = Levels(("A", 5));
        var released = StockAllocator.TryReserve(Order(("A", 2)), levels, Now, TimeSpan.FromHours(1)).Reservation!;
        var expired = StockAllocator.TryReserve(Order(("A", 3)), levels, Now, TimeSpan.FromHours(1)).Reservation!;
        Assert.That(levels["A"].Available, Is.Zero);

        released.Release(levels, Now);
        expired.Expire(levels, Now.AddHours(2));

        Assert.That(levels["A"].Available, Is.EqualTo(5));
        Assert.That(levels["A"].OnHand, Is.EqualTo(5));
        Assert.That(expired.Status, Is.EqualTo(ReservationStatus.Expired));
    }

    [Test]
    public void A_closed_reservation_cannot_be_closed_again()
    {
        var levels = Levels(("A", 5));
        var reservation = StockAllocator.TryReserve(Order(("A", 2)), levels, Now, TimeSpan.FromHours(1)).Reservation!;
        reservation.Commit(levels, Now);

        Assert.That(() => reservation.Release(levels, Now), Throws.InstanceOf<DomainException>().With.Property("Code").EqualTo(InventoryErrors.ReservationClosed));
        Assert.That(levels["A"].OnHand, Is.EqualTo(3));
    }

    [Test]
    public void Reservation_is_due_only_after_its_deadline()
    {
        var levels = Levels(("A", 5));
        var reservation = StockAllocator.TryReserve(Order(("A", 1)), levels, Now, TimeSpan.FromHours(1)).Reservation!;

        Assert.That(reservation.IsDue(Now.AddMinutes(59)), Is.False);
        Assert.That(reservation.IsDue(Now.AddHours(1)), Is.True);
    }
}
