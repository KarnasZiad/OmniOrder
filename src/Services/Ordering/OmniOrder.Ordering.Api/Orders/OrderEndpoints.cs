using OmniOrder.Ordering.Api.Domain;

namespace OmniOrder.Ordering.Api.Orders;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders").WithOpenApi();

        orders.MapPost("/", async (PlaceOrderRequest request, OrderService service, CancellationToken ct) =>
            {
                var order = await service.PlaceAsync(request, ct);
                // 202: the stock reservation is processed asynchronously by the Inventory service.
                return TypedResults.Accepted($"/orders/{order.Id}", order);
            })
            .WithSummary("Checkout (click and collect or reserve in store)")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        orders.MapGet("/", (Guid? storeId, string? customerEmail, OrderStatus? status, OrderService service, CancellationToken ct) =>
                service.SearchAsync(storeId, customerEmail, status, ct))
            .WithSummary("Orders of a store (staff) or of a customer");

        orders.MapGet("/{orderId:guid}", (Guid orderId, OrderService service, CancellationToken ct) => service.GetAsync(orderId, ct))
            .WithSummary("Order tracking")
            .ProducesProblem(StatusCodes.Status404NotFound);

        orders.MapPost("/{orderId:guid}/ready", (Guid orderId, OrderService service, CancellationToken ct) => service.MarkReadyAsync(orderId, ct))
            .WithSummary("Staff: the order is prepared and waiting for the customer")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        orders.MapPost("/{orderId:guid}/collect", (Guid orderId, OrderService service, CancellationToken ct) => service.CollectAsync(orderId, ct))
            .WithSummary("Staff: the customer picked the order up")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        orders.MapPost("/{orderId:guid}/cancel", (Guid orderId, CancelOrderRequest? request, OrderService service, CancellationToken ct) =>
                service.CancelAsync(orderId, request?.Reason, ct))
            .WithSummary("Cancel the order (the reservation is released)")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
