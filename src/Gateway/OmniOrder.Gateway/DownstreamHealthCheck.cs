using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Configuration;

namespace OmniOrder.Gateway;

/// <summary>The gateway is ready when every cluster answers on its <c>/health/ready</c> endpoint.</summary>
internal sealed class DownstreamHealthCheck(IProxyConfigProvider proxyConfig, IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public const string HttpClientName = "health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var destinations = proxyConfig.GetConfig().Clusters
            .SelectMany(c => (c.Destinations ?? new Dictionary<string, DestinationConfig>()).Select(d => (Cluster: c.ClusterId, d.Value.Address)))
            .ToList();

        var results = await Task.WhenAll(destinations.Select(async d =>
        {
            try
            {
                using var response = await client.GetAsync(new Uri(new Uri(d.Address), "/health/ready"), cancellationToken);
                return (d.Cluster, Healthy: response.IsSuccessStatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return (d.Cluster, Healthy: false);
            }
        }));

        var unhealthy = results.Where(r => !r.Healthy).Select(r => r.Cluster).ToList();
        var data = results.ToDictionary(r => r.Cluster, r => (object)(r.Healthy ? "Healthy" : "Unhealthy"));

        return unhealthy.Count == 0
            ? HealthCheckResult.Healthy("All services are ready", data)
            : HealthCheckResult.Unhealthy($"Not ready: {string.Join(", ", unhealthy)}", data: data);
    }
}
