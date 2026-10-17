using MassTransit;
using Microsoft.EntityFrameworkCore;
using OmniOrder.Contracts;
using OmniOrder.Inventory.Api.Data;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Reservations;

/// <summary>Releases the reservations that were not picked up before their deadline.</summary>
public sealed class ReservationExpiryService(InventoryDbContext db, IPublishEndpoint publisher, TimeProvider clock)
{
    public async Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var reservation in due)
        {
            var levels = await db.StockLevelsAsync(reservation.StoreId, reservation.Lines.Select(l => l.Sku), cancellationToken);
            reservation.Expire(levels, now);
            await publisher.Publish(new StockReservationExpired(reservation.OrderId, reservation.StoreId, now), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}

internal sealed partial class ReservationExpiryWorker(
    IServiceScopeFactory scopeFactory,
    Microsoft.Extensions.Options.IOptions<InventoryOptions> options,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ExpiryCheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>().ExpireDueReservationsAsync(stoppingToken);
                if (expired > 0)
                {
                    LogExpired(expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} reservation(s) expired")]
    private partial void LogExpired(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reservation expiry failed")]
    private partial void LogFailed(Exception exception);
}
