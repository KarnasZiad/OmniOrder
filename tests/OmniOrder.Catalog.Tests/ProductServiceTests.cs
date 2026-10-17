using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OmniOrder.Catalog.Api.Data;
using OmniOrder.Catalog.Api.Products;
using OmniOrder.Contracts;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Catalog.Tests;

public sealed class ProductServiceTests
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    [SetUp]
    public async Task SetUp()
    {
        var databaseName = Guid.NewGuid().ToString();
        _provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddDbContext<CatalogDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<ProductService>()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();

        await using var scope = _provider.CreateAsyncScope();
        await CatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private async Task<T> WithServiceAsync<T>(Func<ProductService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ProductService>());
    }

    [Test]
    public async Task Creating_a_product_publishes_ProductCreated()
    {
        var created = await WithServiceAsync(s => s.CreateAsync(new CreateProductRequest("pul-011", "Pull marin", "Laine", "Textile", 69.90m), CancellationToken.None));

        Assert.That(created.Sku, Is.EqualTo("PUL-011"));
        Assert.That(await _harness.Published.Any<ProductCreated>(m => m.Context.Message.Sku == "PUL-011" && m.Context.Message.Price == 69.90m), Is.True);
    }

    [Test]
    public async Task Duplicate_sku_is_rejected_and_nothing_is_published()
    {
        var error = Assert.ThrowsAsync<DomainException>(() =>
            WithServiceAsync(s => s.CreateAsync(new CreateProductRequest("TSH-001", "Copy", null, null, 10m), CancellationToken.None)));

        Assert.That(error!.Code, Is.EqualTo("catalog.duplicate_sku"));
        Assert.That(_harness.Published.Select<ProductCreated>().Any(), Is.False);
    }

    [Test]
    public async Task Search_filters_by_category_and_text()
    {
        var shoes = await WithServiceAsync(s => s.SearchAsync("Chaussures", null, CancellationToken.None));
        var jeans = await WithServiceAsync(s => s.SearchAsync(null, "jean", CancellationToken.None));

        Assert.That(shoes.Select(p => p.Sku), Is.EquivalentTo(new[] { "SNK-006", "BOT-007" }));
        Assert.That(jeans.Select(p => p.Sku), Is.EquivalentTo(new[] { "JEA-002", "VES-004" }));
    }

    [Test]
    public async Task Lookup_returns_the_known_skus_only()
    {
        var found = await WithServiceAsync(s => s.LookupAsync(["tsh-001", "SNK-006", "UNKNOWN"], CancellationToken.None));

        Assert.That(found.Select(p => p.Sku), Is.EquivalentTo(new[] { "TSH-001", "SNK-006" }));
    }

    [Test]
    public void Unknown_product_is_not_found() =>
        Assert.ThrowsAsync<NotFoundException>(() => WithServiceAsync(s => s.GetAsync("NOPE-404", CancellationToken.None)));
}
