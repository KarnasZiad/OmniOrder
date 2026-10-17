using Microsoft.EntityFrameworkCore;
using OmniOrder.Catalog.Api.Data;
using OmniOrder.Catalog.Api.Products;
using OmniOrder.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<CatalogDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Catalog"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("postgresql");

// Catalog only publishes (ProductCreated); it consumes nothing.
builder.AddMessaging<CatalogDbContext>(_ => { });

builder.Services.AddScoped<ProductService>();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();

app.MapProductEndpoints();
app.MapDefaultEndpoints();

await app.Services.InitializeDatabaseAsync<CatalogDbContext>(CatalogSeed.SeedAsync);
await app.RunAsync();

public partial class Program
{
}
