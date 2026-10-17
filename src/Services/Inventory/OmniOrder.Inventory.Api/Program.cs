using Microsoft.EntityFrameworkCore;
using OmniOrder.Inventory.Api;
using OmniOrder.Inventory.Api.Consumers;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Reservations;
using OmniOrder.Inventory.Api.Stock;
using OmniOrder.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<InventoryDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Inventory"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<InventoryDbContext>("postgresql");

builder.AddMessaging<InventoryDbContext>(bus =>
{
    bus.AddConsumer<OrderSubmittedConsumer>();
    bus.AddConsumer<OrderCollectedConsumer>();
    bus.AddConsumer<OrderCancelledConsumer>();
    bus.AddConsumer<ProductCreatedConsumer>();
});

builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection(InventoryOptions.SectionName));
builder.Services.AddScoped<ReservationExpiryService>();
builder.Services.AddHostedService<ReservationExpiryWorker>();

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();

app.MapInventoryEndpoints();
app.MapDefaultEndpoints();

await app.Services.InitializeDatabaseAsync<InventoryDbContext>(InventorySeed.SeedAsync);
await app.RunAsync();

public partial class Program
{
}
