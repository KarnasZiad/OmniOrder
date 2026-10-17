using Microsoft.EntityFrameworkCore;
using OmniOrder.Ordering.Api.Catalog;
using OmniOrder.Ordering.Api.Consumers;
using OmniOrder.Ordering.Api.Data;
using OmniOrder.Ordering.Api.Orders;
using OmniOrder.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<OrderingDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Ordering"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<OrderingDbContext>("postgresql");

builder.AddMessaging<OrderingDbContext>(bus =>
{
    bus.AddConsumer<StockReservedConsumer>();
    bus.AddConsumer<StockReservationFailedConsumer>();
    bus.AddConsumer<StockReservationExpiredConsumer>();
});

builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101"))
    .AddStandardResilienceHandler();

builder.Services.AddScoped<OrderService>();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();

app.MapOrderEndpoints();
app.MapDefaultEndpoints();

await app.Services.InitializeDatabaseAsync<OrderingDbContext>();
await app.RunAsync();

public partial class Program
{
}
