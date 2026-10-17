using Microsoft.AspNetCore.Mvc;

namespace OmniOrder.Catalog.Api.Products;

internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/products").WithTags("Products").WithOpenApi();

        products.MapGet("/", (string? category, string? search, ProductService service, CancellationToken ct) =>
                service.SearchAsync(category, search, ct))
            .WithSummary("Search the active catalogue");

        products.MapGet("/lookup", ([FromQuery(Name = "sku")] string[] skus, ProductService service, CancellationToken ct) =>
                service.LookupAsync(skus, ct))
            .WithSummary("Batch lookup by SKU (used by the Ordering service to price an order)");

        products.MapGet("/{sku}", (string sku, ProductService service, CancellationToken ct) => service.GetAsync(sku, ct))
            .WithSummary("Product details")
            .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPost("/", async (CreateProductRequest request, ProductService service, CancellationToken ct) =>
            {
                var product = await service.CreateAsync(request, ct);
                return TypedResults.Created($"/products/{product.Sku}", product);
            })
            .WithSummary("Create a product (publishes ProductCreated)")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/categories", (ProductService service, CancellationToken ct) => service.CategoriesAsync(ct))
            .WithTags("Products")
            .WithOpenApi();

        return app;
    }
}
