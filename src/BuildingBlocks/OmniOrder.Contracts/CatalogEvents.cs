namespace OmniOrder.Contracts;

/// <summary>Published by Catalog. Inventory initialises a (zero) stock line in every store.</summary>
public sealed record ProductCreated(Guid ProductId, string Sku, string Name, decimal Price, DateTimeOffset OccurredAt);
