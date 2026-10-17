using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Consumers;
using OmniOrder.Inventory.Api.Domain;
using OmniOrder.Inventory.Api.Reservations;

namespace OmniOrder.Inventory.Tests;

public sealed class InventoryConsumerTests
{
    private static readonly Guid Paris = InventoryTestHost.Paris;
    private static readonly Guid Lyon = InventoryTestHost.Lyon;

    private static OrderSubmitted Order(Guid store, FulfillmentMode mode, params (string Sku, int Quantity)[] lines) =>
        new(Guid.NewGuid(), "OO-TEST", store, mode, lines.Select(l => new OrderLineContract(l.Sku, l.Quantity)).ToList(), DateTimeOffset.UtcNow);

    [Test]
    public async Task Submitted_order_is_reserved_in_the_pickup_store()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "TSH-001", 5), (Lyon, "TSH-001", 5));
        var order = Order(Paris, FulfillmentMode.ClickAndCollect, ("TSH-001", 2));

        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);

        var reserved = host.Harness.Published.Select<StockReserved>().Single().Context.Message;
        Assert.That(reserved.OrderId, Is.EqualTo(order.OrderId));
        Assert.That((await host.LevelAsync(Paris, "TSH-001")).Reserved, Is.EqualTo(2));
        Assert.That((await host.LevelAsync(Lyon, "TSH-001")).Reserved, Is.Zero, "other stores are not affected");
    }

    [Test]
    public async Task Hold_duration_depends_on_the_fulfillment_mode()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5));
        var now = host.SharedClock.GetUtcNow();

        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(Order(Paris, FulfillmentMode.ReserveInStore, ("A", 1)));
        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(Order(Paris, FulfillmentMode.ClickAndCollect, ("A", 1)));

        var deadlines = host.Harness.Published.Select<StockReserved>().Select(m => m.Context.Message.ExpiresAt - now).ToList();
        Assert.That(deadlines, Is.EquivalentTo(new[] { TimeSpan.FromDays(2), TimeSpan.FromDays(3) }));
    }

    [Test]
    public async Task Order_is_refused_when_one_line_is_not_available()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5), (Paris, "B", 0));

        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(Order(Paris, FulfillmentMode.ClickAndCollect, ("A", 1), ("B", 1)));

        var failed = host.Harness.Published.Select<StockReservationFailed>().Single().Context.Message;
        Assert.That(failed.UnavailableSkus, Is.EqualTo(new[] { "B" }));
        Assert.That(host.Harness.Published.Select<StockReserved>().Any(), Is.False);
        Assert.That((await host.LevelAsync(Paris, "A")).Reserved, Is.Zero);
    }

    [Test]
    public async Task Redelivered_order_is_reserved_only_once()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5));
        var order = Order(Paris, FulfillmentMode.ClickAndCollect, ("A", 2));

        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);
        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);

        Assert.That((await host.LevelAsync(Paris, "A")).Reserved, Is.EqualTo(2));
        Assert.That(await host.WithDbAsync(db => db.Reservations.CountAsync()), Is.EqualTo(1));
        Assert.That(host.Harness.Published.Select<StockReserved>().Count(), Is.EqualTo(2), "the same answer is sent again");
    }

    [Test]
    public async Task Collected_order_leaves_the_store_stock()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5));
        var order = Order(Paris, FulfillmentMode.ClickAndCollect, ("A", 2));
        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);

        await host.PublishAndWaitAsync<OrderCollectedConsumer, OrderCollected>(new OrderCollected(order.OrderId, Paris, DateTimeOffset.UtcNow));

        var level = await host.LevelAsync(Paris, "A");
        Assert.That((level.OnHand, level.Reserved), Is.EqualTo((3, 0)));
    }

    [Test]
    public async Task Cancelled_order_releases_its_reservation()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5));
        var order = Order(Paris, FulfillmentMode.ReserveInStore, ("A", 5));
        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);

        await host.PublishAndWaitAsync<OrderCancelledConsumer, OrderCancelled>(new OrderCancelled(order.OrderId, Paris, "Customer request", DateTimeOffset.UtcNow));

        var level = await host.LevelAsync(Paris, "A");
        Assert.That((level.OnHand, level.Available), Is.EqualTo((5, 5)));
    }

    [Test]
    public async Task New_product_is_referenced_in_every_store()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "NEW-1", 4));

        await host.PublishAndWaitAsync<ProductCreatedConsumer, ProductCreated>(new ProductCreated(Guid.NewGuid(), "NEW-1", "New", 10m, DateTimeOffset.UtcNow));

        Assert.That((await host.LevelAsync(Paris, "NEW-1")).OnHand, Is.EqualTo(4), "existing line untouched");
        Assert.That((await host.LevelAsync(Lyon, "NEW-1")).OnHand, Is.Zero);
    }

    [Test]
    public async Task Reservations_not_picked_up_in_time_expire()
    {
        await using var host = await InventoryTestHost.StartAsync((Paris, "A", 5));
        var order = Order(Paris, FulfillmentMode.ReserveInStore, ("A", 3));
        await host.PublishAndWaitAsync<OrderSubmittedConsumer, OrderSubmitted>(order);

        host.SharedClock.Advance(TimeSpan.FromDays(2) + TimeSpan.FromMinutes(1));
        await using (var scope = host.Provider.CreateAsyncScope())
        {
            var expired = await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>().ExpireDueReservationsAsync(CancellationToken.None);
            Assert.That(expired, Is.EqualTo(1));
        }

        Assert.That(await host.Harness.Published.Any<StockReservationExpired>(m => m.Context.Message.OrderId == order.OrderId), Is.True);
        Assert.That((await host.LevelAsync(Paris, "A")).Available, Is.EqualTo(5));
        var reservation = await host.WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        Assert.That(reservation.Status, Is.EqualTo(ReservationStatus.Expired));
    }
}
