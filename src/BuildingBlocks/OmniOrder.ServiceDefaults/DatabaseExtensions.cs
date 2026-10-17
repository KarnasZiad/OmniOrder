using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OmniOrder.ServiceDefaults;

public static class DatabaseExtensions
{
    /// <summary>
    /// Creates the service schema on first start, then runs the optional seed.
    /// Each service owns its own database (database-per-service).
    /// </summary>
    public static async Task InitializeDatabaseAsync<TDbContext>(
        this IServiceProvider services,
        Func<TDbContext, CancellationToken, Task>? seed = null,
        CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (seed is not null)
        {
            await seed(db, cancellationToken);
        }
    }
}
