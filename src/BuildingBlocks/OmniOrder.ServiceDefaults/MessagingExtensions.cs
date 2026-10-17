using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OmniOrder.ServiceDefaults;

public static class MessagingExtensions
{
    /// <summary>
    /// RabbitMQ through MassTransit, with the Entity Framework transactional outbox:
    /// <list type="bullet">
    ///   <item>messages published by an HTTP endpoint are stored with the business data in the same transaction, then delivered (bus outbox);</item>
    ///   <item>consumers are idempotent: a redelivered message is detected by the inbox and not processed twice;</item>
    ///   <item>transient failures (optimistic concurrency...) are retried.</item>
    /// </list>
    /// </summary>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator> configureConsumers)
        where TDbContext : DbContext
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            configureConsumers(bus);

            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });

            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            bus.UsingRabbitMq((context, rabbit) =>
            {
                var settings = builder.Configuration.GetSection("RabbitMq");
                rabbit.Host(settings["Host"] ?? "localhost", settings["VirtualHost"] ?? "/", host =>
                {
                    host.Username(settings["Username"] ?? "guest");
                    host.Password(settings["Password"] ?? "guest");
                });
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    /// <summary>Adds the outbox/inbox tables to a service DbContext.</summary>
    public static ModelBuilder AddMessagingTables(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        return modelBuilder;
    }
}
