using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OmniOrder.Ordering.Api.Catalog;
using OmniOrder.Ordering.Api.Consumers;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Orders;

namespace OmniOrder.Ordering.Tests;

internal sealed class FakeCatalog : ICatalogClient
{
    public Dictionary<string, CatalogProduct> Products { get; } = new()
    {
        ["TSH-001"] = new(Guid.NewGuid(), "TSH-001", "T-shirt coton bio", 19.90m),
        ["SNK-006"] = new(Guid.NewGuid(), "SNK-006", "Sneakers cuir blanc", 89.90m),
    };

    public Task<IReadOnlyList<CatalogProduct>> LookupAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogProduct>>(skus.Where(Products.ContainsKey).Select(s => Products[s]).ToList());
}

internal sealed class OrderingTestHost : IAsyncDisposable
{
    private OrderingTestHost(ServiceProvider provider)
    {
        Provider = provider;
        Harness = provider.GetRequiredService<ITestHarness>();
    }

    public ServiceProvider Provider { get; }

    public ITestHarness Harness { get; }

    public static async Task<OrderingTestHost> StartAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero)))
            .AddSingleton<ICatalogClient, FakeCatalog>()
            .AddDbContext<OrderingDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<OrderService>()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<StockReservedConsumer>();
                bus.AddConsumer<StockReservationFailedConsumer>();
                bus.AddConsumer<StockReservationExpiredConsumer>();
            })
            .BuildServiceProvider(true);

        var host = new OrderingTestHost(provider);
        await host.Harness.Start();
        return host;
    }

    public async Task<T> WithServiceAsync<T>(Func<OrderService, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<OrderService>());
    }

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
