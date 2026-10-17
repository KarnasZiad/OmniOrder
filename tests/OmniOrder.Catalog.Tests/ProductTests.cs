using OmniOrder.Catalog.Api.Domain;
using OmniOrder.ServiceDefaults;

namespace OmniOrder.Catalog.Tests;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public void Sku_is_normalised()
    {
        var product = new Product(" tsh-001 ", "T-shirt", "", "Textile", 19.9m, Now);

        Assert.That(product.Sku, Is.EqualTo("TSH-001"));
    }

    [TestCase("")]
    [TestCase("AB")]
    [TestCase("WITH SPACE")]
    [TestCase("TOO-LONG-SKU-1234567890")]
    public void Invalid_sku_is_rejected(string sku) =>
        Assert.That(
            () => new Product(sku, "Name", "", "Cat", 10m, Now),
            Throws.InstanceOf<DomainException>().With.Property("Code").EqualTo("catalog.invalid_sku"));

    [TestCase(0)]
    [TestCase(-5)]
    public void Price_must_be_positive(decimal price) =>
        Assert.That(() => new Product("SKU-1", "Name", "", "Cat", price, Now), Throws.InstanceOf<DomainException>());

    [Test]
    public void Name_is_required() =>
        Assert.That(() => new Product("SKU-1", " ", "", "Cat", 10m, Now), Throws.InstanceOf<DomainException>());

    [Test]
    public void Missing_category_defaults_to_misc()
    {
        var product = new Product("SKU-1", "Name", "", " ", 10m, Now);

        Assert.That(product.Category, Is.EqualTo("Divers"));
    }
}
