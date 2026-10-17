using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OmniOrder.ServiceDefaults;

/// <summary>Maps business exceptions to RFC 7807 problem details (404, 409, 422).</summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent modification", "concurrency_conflict"),
            DomainException domain => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", domain.Code),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request", "bad_request"),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
