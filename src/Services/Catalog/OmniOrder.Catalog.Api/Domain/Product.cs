using System.Text.RegularExpressions;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Catalog.Api.Domain;

public sealed partial class Product
{
    private Product()
    {
    }

    public Product(string sku, string name, string description, string category, decimal price, DateTimeOffset createdAt)
    {
        sku = sku?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SkuFormat().IsMatch(sku))
        {
            throw new DomainException("catalog.invalid_sku", "SKU must be 3 to 20 characters: uppercase letters, digits or dashes.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("catalog.name_required", "Product name is required.");
        }

        if (price <= 0)
        {
            throw new DomainException("catalog.invalid_price", "Price must be greater than zero.");
        }

        Id = Guid.NewGuid();
        Sku = sku;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(category) ? "Divers" : category.Trim();
        Price = decimal.Round(price, 2);
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    [GeneratedRegex("^[A-Z0-9-]{3,20}$")]
    private static partial Regex SkuFormat();
}
