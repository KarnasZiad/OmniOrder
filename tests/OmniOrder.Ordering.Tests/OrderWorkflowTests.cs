using OmniOrder.Contracts;
using OmniOrder.Ordering.Api.Consumers;
using OmniOrder.Ordering.Api.Domain;
using OmniOrder.Ordering.Api.Orders;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Ordering.Tests;

public sealed class OrderWorkflowTests
{
    private static readonly Guid Store = Guid.NewGuid();

    private static PlaceOrderRequest Checkout(FulfillmentMode mode = FulfillmentMode.ClickAndCollect, params OrderLineRequest[] lines) =>
        new(Store, mode, "Camille Martin", "camille@example.com", lines.Length > 0 ? lines : [new("tsh-001", 2), new("SNK-006", 1)]);

    [Test]
    public async Task Checkout_prices_the_order_with_the_catalogue_and_asks_for_a_reservation()
    {
        await using var host = await OrderingTestHost.StartAsync();

        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(), CancellationToken.None));

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
        Assert.That(order.Total, Is.EqualTo(2 * 19.90m + 89.90m));
        Assert.That(order.Items.Select(i => i.Sku), Is.EquivalentTo(new[] { "TSH-001", "SNK-006" }));

        var submitted = host.Harness.Published.Select<OrderSubmitted>().Single().Context.Message;
        Assert.That(submitted.OrderId, Is.EqualTo(order.Id));
        Assert.That(submitted.StoreId, Is.EqualTo(Store));
        Assert.That(submitted.Lines, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Checkout_with_an_unknown_product_is_refused()
    {
        await using var host = await OrderingTestHost.StartAsync();

        var error = Assert.ThrowsAsync<DomainException>(() =>
            host.WithServiceAsync(s => s.PlaceAsync(Checkout(FulfillmentMode.ClickAndCollect, new OrderLineRequest("NOPE-1", 1)), CancellationToken.None)));

        Assert.That(error!.Code, Is.EqualTo(OrderErrors.UnknownProducts));
        Assert.That(host.Harness.Published.Select<OrderSubmitted>().Any(), Is.False);
    }

    [TestCase(FulfillmentMode.ClickAndCollect, OrderStatus.Confirmed)]
    [TestCase(FulfillmentMode.ReserveInStore, OrderStatus.Reserved)]
    public async Task Reserved_stock_confirms_the_order(FulfillmentMode mode, OrderStatus expected)
    {
        await using var host = await OrderingTestHost.StartAsync();
        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(mode), CancellationToken.None));

        await host.PublishAndWaitAsync<StockReservedConsumer, StockReserved>(new StockReserved(order.Id, Store, DateTimeOffset.UtcNow.AddDays(2)));

        var reloaded = await host.WithServiceAsync(s => s.GetAsync(order.Id, CancellationToken.None));
        Assert.That(reloaded.Status, Is.EqualTo(expected));
        Assert.That(reloaded.PickupDeadline, Is.Not.Null);
    }

    [Test]
    public async Task Failed_reservation_rejects_the_order_with_the_reason()
    {
        await using var host = await OrderingTestHost.StartAsync();
        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(), CancellationToken.None));

        await host.PublishAndWaitAsync<StockReservationFailedConsumer, StockReservationFailed>(
            new StockReservationFailed(order.Id, Store, "Insufficient stock", ["SNK-006"]));

        var reloaded = await host.WithServiceAsync(s => s.GetAsync(order.Id, CancellationToken.None));
        Assert.That(reloaded.Status, Is.EqualTo(OrderStatus.Rejected));
        Assert.That(reloaded.Payment, Is.EqualTo(PaymentStatus.Refunded));
        Assert.That(reloaded.StatusReason, Does.Contain("SNK-006"));
    }

    [Test]
    public async Task Late_reservation_of_a_cancelled_order_is_compensated()
    {
        await using var host = await OrderingTestHost.StartAsync();
        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(), CancellationToken.None));
        await host.WithServiceAsync(s => s.CancelAsync(order.Id, "Changed my mind", CancellationToken.None));

        await host.PublishAndWaitAsync<StockReservedConsumer, StockReserved>(new StockReserved(order.Id, Store, DateTimeOffset.UtcNow.AddDays(2)));

        // One OrderCancelled from the cancellation, one from the compensation.
        Assert.That(host.Harness.Published.Select<OrderCancelled>().Count(m => m.Context.Message.OrderId == order.Id), Is.EqualTo(2));
        var reloaded = await host.WithServiceAsync(s => s.GetAsync(order.Id, CancellationToken.None));
        Assert.That(reloaded.Status, Is.EqualTo(OrderStatus.Cancelled));
    }

    [Test]
    public async Task Staff_workflow_publishes_ready_and_collected_events()
    {
        await using var host = await OrderingTestHost.StartAsync();
        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(), CancellationToken.None));
        await host.PublishAndWaitAsync<StockReservedConsumer, StockReserved>(new StockReserved(order.Id, Store, DateTimeOffset.UtcNow.AddDays(2)));

        await host.WithServiceAsync(s => s.MarkReadyAsync(order.Id, CancellationToken.None));
        var collected = await host.WithServiceAsync(s => s.CollectAsync(order.Id, CancellationToken.None));

        Assert.That(collected.Status, Is.EqualTo(OrderStatus.Collected));
        Assert.That(await host.Harness.Published.Any<OrderReadyForPickup>(), Is.True);
        Assert.That(await host.Harness.Published.Any<OrderCollected>(m => m.Context.Message.OrderId == order.Id), Is.True);
    }

    [Test]
    public async Task Expired_reservation_expires_the_order()
    {
        await using var host = await OrderingTestHost.StartAsync();
        var order = await host.WithServiceAsync(s => s.PlaceAsync(Checkout(FulfillmentMode.ReserveInStore), CancellationToken.None));
        await host.PublishAndWaitAsync<StockReservedConsumer, StockReserved>(new StockReserved(order.Id, Store, DateTimeOffset.UtcNow.AddDays(2)));

        await host.PublishAndWaitAsync<StockReservationExpiredConsumer, StockReservationExpired>(new StockReservationExpired(order.Id, Store, DateTimeOffset.UtcNow));

        var reloaded = await host.WithServiceAsync(s => s.GetAsync(order.Id, CancellationToken.None));
        Assert.That(reloaded.Status, Is.EqualTo(OrderStatus.Expired));
        Assert.That(reloaded.Payment, Is.EqualTo(PaymentStatus.None));
    }

    [Test]
    public async Task Customers_and_stores_see_their_own_orders()
    {
        await using var host = await OrderingTestHost.StartAsync();
        await host.WithServiceAsync(s => s.PlaceAsync(Checkout(), CancellationToken.None));
        await host.WithServiceAsync(s => s.PlaceAsync(Checkout() with { CustomerEmail = "other@example.com", StoreId = Guid.NewGuid() }, CancellationToken.None));

        var mine = await host.WithServiceAsync(s => s.SearchAsync(null, "CAMILLE@example.com", null, CancellationToken.None));
        var store = await host.WithServiceAsync(s => s.SearchAsync(Store, null, OrderStatus.Pending, CancellationToken.None));

        Assert.That(mine, Has.Count.EqualTo(1));
        Assert.That(store, Has.Count.EqualTo(1));
    }
}
