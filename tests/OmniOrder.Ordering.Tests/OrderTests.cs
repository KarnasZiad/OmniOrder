using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Ordering.Tests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid Store = Guid.NewGuid();

    private static readonly PricedLine[] Cart =
    [
        new("TSH-001", "T-shirt", 19.90m, 2),
        new("SNK-006", "Sneakers", 89.90m, 1),
    ];

    private static Order Place(FulfillmentMode mode = FulfillmentMode.ClickAndCollect) =>
        Order.Place(Store, mode, "Camille Martin", "Camille.Martin@example.com", Cart, Now);

    private static Order InStatus(OrderStatus status, FulfillmentMode mode = FulfillmentMode.ClickAndCollect)
    {
        var order = Place(mode);
        switch (status)
        {
            case OrderStatus.Pending:
                break;
            case OrderStatus.Confirmed or OrderStatus.Reserved:
                order.ConfirmStock(Now.AddDays(2), Now);
                break;
            case OrderStatus.ReadyForPickup:
                order.ConfirmStock(Now.AddDays(2), Now);
                order.MarkReadyForPickup(Now);
                break;
            case OrderStatus.Collected:
                order.ConfirmStock(Now.AddDays(2), Now);
                order.MarkReadyForPickup(Now);
                order.Collect(Now);
                break;
            case OrderStatus.Rejected:
                order.Reject("No stock", Now);
                break;
            case OrderStatus.Cancelled:
                order.Cancel("Changed my mind", Now);
                break;
            case OrderStatus.Expired:
                order.ConfirmStock(Now.AddDays(2), Now);
                order.Expire(Now.AddDays(3));
                break;
        }

        return order;
    }

    [Test]
    public void A_placed_order_is_pending_and_priced()
    {
        var order = Place();

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
        Assert.That(order.Total, Is.EqualTo(129.70m));
        Assert.That(order.CustomerEmail, Is.EqualTo("camille.martin@example.com"));
        Assert.That(order.Number, Does.Match("^OO-261017-[0-9A-F]{6}$"));
    }

    [TestCase(FulfillmentMode.ClickAndCollect, PaymentStatus.Captured)]
    [TestCase(FulfillmentMode.ReserveInStore, PaymentStatus.DueInStore)]
    public void Payment_depends_on_the_fulfillment_mode(FulfillmentMode mode, PaymentStatus expected) =>
        Assert.That(Place(mode).Payment, Is.EqualTo(expected));

    [Test]
    public void An_order_needs_a_store_a_customer_and_valid_lines()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Order.Place(Guid.Empty, FulfillmentMode.ClickAndCollect, "A", "a@b.c", Cart, Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => Order.Place(Store, FulfillmentMode.ClickAndCollect, "A", "not-an-email", Cart, Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => Order.Place(Store, FulfillmentMode.ClickAndCollect, "A", "a@b.c", [], Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => Order.Place(Store, FulfillmentMode.ClickAndCollect, "A", "a@b.c", [new("A", "A", 1m, 0)], Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => Order.Place(Store, FulfillmentMode.ClickAndCollect, "A", "a@b.c", [new("A", "A", 1m, 11)], Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => Order.Place(Store, FulfillmentMode.ClickAndCollect, "A", "a@b.c", [new("A", "A", 1m, 1), new("A", "A", 1m, 1)], Now), Throws.InstanceOf<DomainException>());
        });
    }

    [TestCase(FulfillmentMode.ClickAndCollect, OrderStatus.Confirmed)]
    [TestCase(FulfillmentMode.ReserveInStore, OrderStatus.Reserved)]
    public void Reserved_stock_confirms_the_order(FulfillmentMode mode, OrderStatus expected)
    {
        var order = Place(mode);

        order.ConfirmStock(Now.AddDays(3), Now);

        Assert.That(order.Status, Is.EqualTo(expected));
        Assert.That(order.PickupDeadline, Is.EqualTo(Now.AddDays(3)));
    }

    [Test]
    public void Click_and_collect_happy_path()
    {
        var order = Place();
        order.ConfirmStock(Now.AddDays(3), Now);
        order.MarkReadyForPickup(Now.AddHours(2));
        order.Collect(Now.AddDays(1));

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Collected));
        Assert.That(order.Payment, Is.EqualTo(PaymentStatus.Captured));
        Assert.That(order.IsFinal, Is.True);
    }

    [Test]
    public void Reserve_in_store_is_paid_at_the_counter()
    {
        var order = InStatus(OrderStatus.ReadyForPickup, FulfillmentMode.ReserveInStore);

        order.Collect(Now);

        Assert.That(order.Payment, Is.EqualTo(PaymentStatus.PaidInStore));
    }

    [Test]
    public void Rejected_click_and_collect_is_refunded()
    {
        var order = InStatus(OrderStatus.Rejected);

        Assert.That(order.Payment, Is.EqualTo(PaymentStatus.Refunded));
        Assert.That(order.StatusReason, Is.EqualTo("No stock"));
    }

    [TestCase(OrderStatus.Pending)]
    [TestCase(OrderStatus.Confirmed)]
    [TestCase(OrderStatus.ReadyForPickup)]
    public void Active_orders_can_be_cancelled(OrderStatus status)
    {
        var order = InStatus(status);

        order.Cancel("Customer request", Now);

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(order.Payment, Is.EqualTo(PaymentStatus.Refunded));
    }

    [TestCase(OrderStatus.Collected)]
    [TestCase(OrderStatus.Rejected)]
    [TestCase(OrderStatus.Cancelled)]
    [TestCase(OrderStatus.Expired)]
    public void Final_orders_cannot_change_anymore(OrderStatus status)
    {
        var order = InStatus(status);

        Assert.Multiple(() =>
        {
            Assert.That(() => order.Cancel("x", Now), Throws.InstanceOf<DomainException>().With.Property("Code").EqualTo(OrderErrors.InvalidTransition));
            Assert.That(() => order.MarkReadyForPickup(Now), Throws.InstanceOf<DomainException>());
            Assert.That(() => order.Collect(Now), Throws.InstanceOf<DomainException>());
        });
    }

    [Test]
    public void An_order_must_be_prepared_before_it_is_collected()
    {
        var pending = InStatus(OrderStatus.Pending);
        var confirmed = InStatus(OrderStatus.Confirmed);

        Assert.That(() => pending.MarkReadyForPickup(Now), Throws.InstanceOf<DomainException>());
        Assert.That(() => confirmed.Collect(Now), Throws.InstanceOf<DomainException>());
    }

    [Test]
    public void A_reservation_cannot_expire_before_it_is_confirmed() =>
        Assert.That(() => InStatus(OrderStatus.Pending).Expire(Now), Throws.InstanceOf<DomainException>());
}
