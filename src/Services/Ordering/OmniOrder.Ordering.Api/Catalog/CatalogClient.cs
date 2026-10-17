using System.Net.Http.Json;

namespace OmniOrder.Ordering.Api.Catalog;

public sealed record CatalogProduct(Guid Id, string Sku, string Name, decimal Price);

public interface ICatalogClient
{
    Task<IReadOnlyList<CatalogProduct>> LookupAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);
}

/// <summary>
/// Synchronous call to the Catalog service to price the order with the reference prices
/// (never trust prices sent by the browser). Protected by the standard resilience pipeline
/// (timeout, retries with jitter, circuit breaker).
/// </summary>
internal sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<IReadOnlyList<CatalogProduct>> LookupAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        var query = string.Join('&', skus.Select(s => $"sku={Uri.EscapeDataString(s)}"));
        return await http.GetFromJsonAsync<List<CatalogProduct>>($"/products/lookup?{query}", cancellationToken) ?? [];
    }
}
