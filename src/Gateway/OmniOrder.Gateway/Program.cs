using System.Threading.RateLimiting;
using OmniOrder.Gateway;
using OmniOrder.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Single entry point for the front-ends: routing, active health checks of the destinations,
// CORS and rate limiting are handled here, not in every service.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient(DownstreamHealthCheck.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddHealthChecks().AddCheck<DownstreamHealthCheck>("downstream-services");

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Checkout protection: 20 orders per minute and per client IP.
    options.AddPolicy("checkout", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();
app.MapReverseProxy();
app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Text("OmniOrder API Gateway: /api/catalog, /api/ordering, /api/inventory")).ExcludeFromDescription();

await app.RunAsync();
