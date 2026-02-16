using Custom.Framework.Billing.Consumers;
using Custom.Framework.Billing.Sagas;
using Custom.Framework.Billing.Services;
using Custom.Framework.Billing.StateMachines;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Billing;

/// <summary>
/// Options for configuring the Billing module with MassTransit
/// </summary>
public class BillingOptions
{
    public string RabbitMqHost { get; set; } = "localhost";
    public int RabbitMqPort { get; set; } = 5672;
    public string RabbitMqUsername { get; set; } = "guest";
    public string RabbitMqPassword { get; set; } = "guest";
    public string QueueName { get; set; } = "billing_queue";
}

/// <summary>
/// Extension methods for registering Billing module with MassTransit
/// </summary>
public static class BillingExtensions
{
    /// <summary>
    /// Add Billing module with MassTransit RabbitMQ support
    /// </summary>
    public static IServiceCollection AddBillingWithMassTransit(
        this IServiceCollection services,
        Action<BillingOptions>? configure = null)
    {
        var options = new BillingOptions();
        configure?.Invoke(options);

        // Register services
        services.AddSingleton<IBillingService, BillingService>();
        services.AddSingleton<FlightService>();
        services.AddSingleton<HotelService>();
        services.AddSingleton<CarRentalService>();
        services.AddSingleton<TransactionStateMachine>();
        services.AddSingleton<TravelBookingSaga>();

        // Configure MassTransit with RabbitMQ
        services.AddMassTransit(x =>
        {
            // Register consumers
            x.AddConsumer<DepositCommandConsumer>();
            x.AddConsumer<WithdrawCommandConsumer>();
            x.AddConsumer<CreateSubscriptionCommandConsumer>();
            x.AddConsumer<CancelSubscriptionCommandConsumer>();
            x.AddConsumer<BookTravelCommandConsumer>();
            x.AddConsumer<DeadLetterQueueConsumer>();

            // Configure RabbitMQ
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.RabbitMqHost, h =>
                {
                    h.Username(options.RabbitMqUsername);
                    h.Password(options.RabbitMqPassword);
                });

                // Configure receive endpoints for consumers
                cfg.ReceiveEndpoint(options.QueueName, e =>
                {
                    e.ConfigureConsumer<DepositCommandConsumer>(context);
                    e.ConfigureConsumer<WithdrawCommandConsumer>(context);
                    e.ConfigureConsumer<CreateSubscriptionCommandConsumer>(context);
                    e.ConfigureConsumer<CancelSubscriptionCommandConsumer>(context);
                    e.ConfigureConsumer<BookTravelCommandConsumer>(context);

                    // Configure retry policy
                    e.UseMessageRetry(r => r.Intervals(100, 500, 1000));

                    // Configure circuit breaker
                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 10;
                        cb.ResetInterval = TimeSpan.FromMinutes(5);
                    });
                });

                // Configure Dead Letter Queue endpoint (no retries for DLQ events)
                cfg.ReceiveEndpoint("billing_dlq", e =>
                {
                    e.ConfigureConsumer<DeadLetterQueueConsumer>(context);

                    // Don't retry DLQ messages - they're already failed compensations
                    e.UseMessageRetry(r => r.None());
                });

                // Configure message topology
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Add Billing module without MassTransit (for testing or simple scenarios)
    /// </summary>
    public static IServiceCollection AddBillingServices(this IServiceCollection services)
    {
        services.AddSingleton<IBillingService, BillingService>();
        services.AddSingleton<FlightService>();
        services.AddSingleton<HotelService>();
        services.AddSingleton<CarRentalService>();
        services.AddSingleton<TransactionStateMachine>();
        services.AddSingleton<TravelBookingSaga>();

        return services;
    }
}
