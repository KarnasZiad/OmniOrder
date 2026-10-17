using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OmniOrder.Inventory.Api;
using OmniOrder.Inventory.Api.Consumers;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;
using OmniOrder.Inventory.Api.Reservations;

namespace OmniOrder.Inventory.Tests;

/// <summary>Inventory consumers wired on the MassTransit in-memory test harness and an in-memory database.</summary>
internal sealed class InventoryTestHost : IAsyncDisposable
{
    public static readonly Guid Paris = Guid.NewGuid();
    public static readonly Guid Lyon = Guid.NewGuid();

    private InventoryTestHost(ServiceProvider provider)
    {
        Provider = provider;
        Harness = provider.GetRequiredService<ITestHarness>();
    }

    public ServiceProvider Provider { get; }

    public ITestHarness Harness { get; }

    public static async Task<InventoryTestHost> StartAsync(params (Guid Store, string Sku, int OnHand)[] stock)
    {
        var databaseName = Guid.NewGuid().ToString();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(clock)
            .Configure<InventoryOptions>(_ => { })
            .AddDbContext<InventoryDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<ReservationExpiryService>()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<OrderSubmittedConsumer>();
                bus.AddConsumer<OrderCollectedConsumer>();
                bus.AddConsumer<OrderCancelledConsumer>();
                bus.AddConsumer<ProductCreatedConsumer>();
            })
            .BuildServiceProvider(true);

        var host = new InventoryTestHost(provider);
        await host.WithDbAsync(async db =>
        {
            db.Stores.AddRange(
                new Store(Paris, "PAR", "Paris", "Paris", "1 rue de Rivoli"),
                new Store(Lyon, "LYO", "Lyon", "Lyon", "1 rue de la Republique"));
            db.StockLevels.AddRange(stock.Select(s => new StockLevel(s.Store, s.Sku, s.OnHand)));
            await db.SaveChangesAsync();
        });

        await host.Harness.Start();
        return host;
    }

    public FakeTimeProvider SharedClock => (FakeTimeProvider)Provider.GetRequiredService<TimeProvider>();

    public async Task WithDbAsync(Func<InventoryDbContext, Task> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<InventoryDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<InventoryDbContext, Task<T>> query)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<InventoryDbContext>());
    }

    public Task<StockLevel> LevelAsync(Guid store, string sku) =>
        WithDbAsync(db => db.StockLevels.AsNoTracking().SingleAsync(s => s.StoreId == store && s.Sku == sku));

    /// <summary>Publishes a message and waits until the given consumer has processed it.</summary>
    public async Task PublishAndWaitAsync<TConsumer, TMessage>(TMessage message)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        var consumer = Harness.GetConsumerHarness<TConsumer>();
        var before = consumer.Consumed.Select<TMessage>().Count();
        await Harness.Bus.Publish(message);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (consumer.Consumed.Select<TMessage>().Count() == before)
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), $"{typeof(TConsumer).Name} did not consume {typeof(TMessage).Name}");
            await Task.Delay(20);
        }

        await Harness.InactivityTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await Provider.DisposeAsync();
    }
}
