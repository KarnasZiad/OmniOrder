using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Catalog.Api.Data;
using OmniOrder.Catalog.Api.Domain;
using OmniOrder.Contracts;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Catalog.Api.Products;

public sealed record ProductDto(Guid Id, string Sku, string Name, string Description, string Category, decimal Price)
{
    public static ProductDto From(Product p) => new(p.Id, p.Sku, p.Name, p.Description, p.Category, p.Price);
}

public sealed record CreateProductRequest(string Sku, string Name, string? Description, string? Category, decimal Price);

public sealed class ProductService(CatalogDbContext db, IPublishEndpoint publisher, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string? category, string? search, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        var products = await query.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync(cancellationToken);
        return products.Select(ProductDto.From).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> LookupAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        var normalized = skus.Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && normalized.Contains(p.Sku))
            .ToListAsync(cancellationToken);
        return products.Select(ProductDto.From).ToList();
    }

    public async Task<ProductDto> GetAsync(string sku, CancellationToken cancellationToken)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Sku == normalized, cancellationToken)
            ?? throw new NotFoundException("Product", normalized);
        return ProductDto.From(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Sku, request.Name, request.Description ?? string.Empty, request.Category ?? string.Empty, request.Price, clock.GetUtcNow());

        if (await db.Products.AnyAsync(p => p.Sku == product.Sku, cancellationToken))
        {
            throw new DomainException("catalog.duplicate_sku", $"A product with SKU '{product.Sku}' already exists.");
        }

        db.Products.Add(product);

        // Stored in the outbox with the product (same transaction), delivered to RabbitMQ afterwards.
        await publisher.Publish(new ProductCreated(product.Id, product.Sku, product.Name, product.Price, product.CreatedAt), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }

    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
}
