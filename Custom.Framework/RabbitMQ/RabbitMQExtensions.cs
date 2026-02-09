using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.RabbitMQ;

/// <summary>
/// Extension methods for configuring RabbitMQ services
/// </summary>
public static class RabbitMQExtensions
{
    /// <summary>
    /// Add RabbitMQ publisher to the service collection
    /// </summary>
    public static IServiceCollection AddRabbitMQPublisher(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RabbitMQ")
    {
        // Bind configuration
        var options = configuration.GetSection(sectionName).Get<RabbitMQOptions>() 
            ?? new RabbitMQOptions();
        
        services.AddSingleton(options);

        // Register publisher as singleton with async initialization
        services.AddSingleton<IRabbitMQPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQPublisher>>();
            return RabbitMQPublisher.CreateAsync(options, logger).GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Add RabbitMQ publisher with custom configuration
    /// </summary>
    public static IServiceCollection AddRabbitMQPublisher(
        this IServiceCollection services,
        Action<RabbitMQOptions> configureOptions)
    {
        var options = new RabbitMQOptions();
        configureOptions(options);
        
        services.AddSingleton(options);

        services.AddSingleton<IRabbitMQPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQPublisher>>();
            return RabbitMQPublisher.CreateAsync(options, logger).GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Add generic typed publisher for specific message type
    /// </summary>
    public static IServiceCollection AddRabbitMQPublisher<TMessage>(
        this IServiceCollection services,
        string exchange,
        string routingKey) where TMessage : class
    {
        services.AddSingleton<IRabbitMQPublisher<TMessage>>(sp =>
        {
            var publisher = sp.GetRequiredService<IRabbitMQPublisher>();
            return new RabbitMQPublisher<TMessage>(publisher, exchange, routingKey);
        });

        return services;
    }

    /// <summary>
    /// Add RabbitMQ subscriber to the service collection
    /// </summary>
    public static IServiceCollection AddRabbitMQSubscriber(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RabbitMQ")
    {
        // Get or add options
        var options = configuration.GetSection(sectionName).Get<RabbitMQOptions>() 
            ?? services.BuildServiceProvider().GetService<RabbitMQOptions>()
            ?? new RabbitMQOptions();

        if (!services.Any(x => x.ServiceType == typeof(RabbitMQOptions)))
        {
            services.AddSingleton(options);
        }

        // Register subscriber as singleton with async initialization
        services.AddSingleton<IRabbitMQSubscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQSubscriber>>();
            return RabbitMQSubscriber.CreateAsync(options, logger).GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Add RabbitMQ subscriber with custom configuration
    /// </summary>
    public static IServiceCollection AddRabbitMQSubscriber(
        this IServiceCollection services,
        Action<RabbitMQOptions> configureOptions)
    {
        var options = services.BuildServiceProvider().GetService<RabbitMQOptions>() ?? new RabbitMQOptions();
        configureOptions(options);

        if (!services.Any(x => x.ServiceType == typeof(RabbitMQOptions)))
        {
            services.AddSingleton(options);
        }

        services.AddSingleton<IRabbitMQSubscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQSubscriber>>();
            return RabbitMQSubscriber.CreateAsync(options, logger).GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Add generic typed subscriber for specific message type
    /// </summary>
    public static IServiceCollection AddRabbitMQSubscriber<TMessage>(
        this IServiceCollection services,
        string queue) where TMessage : class
    {
        services.AddSingleton<IRabbitMQSubscriber<TMessage>>(sp =>
        {
            var subscriber = sp.GetRequiredService<IRabbitMQSubscriber>();
            return new RabbitMQSubscriber<TMessage>(subscriber, queue);
        });

        return services;
    }

    /// <summary>
    /// Initialize RabbitMQ on application startup
    /// For ASP.NET Core applications
    /// </summary>
    public static IHost UseRabbitMQ(this IHost host)
    {
        using var scope = host.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetService<IRabbitMQPublisher>();
        if (publisher != null && publisher.IsHealthy())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<RabbitMQPublisher>>();
            logger.LogInformation("✅ RabbitMQ Publisher is healthy and ready");
        }

        var subscriber = scope.ServiceProvider.GetService<IRabbitMQSubscriber>();
        if (subscriber != null && subscriber.IsHealthy())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<RabbitMQSubscriber>>();
            logger.LogInformation("✅ RabbitMQ Subscriber is healthy and ready");
        }

        return host;
    }
}

/// <summary>
/// Background service for ensuring RabbitMQ is initialized on startup
/// Use this for hosted services / worker services
/// </summary>
public class RabbitMQInitializer : IHostedService
{
    private readonly ILogger<RabbitMQInitializer> _logger;
    private readonly IRabbitMQPublisher? _publisher;
    private readonly IRabbitMQSubscriber? _subscriber;

    public RabbitMQInitializer(
        ILogger<RabbitMQInitializer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _publisher = serviceProvider.GetService<IRabbitMQPublisher>();
        _subscriber = serviceProvider.GetService<IRabbitMQSubscriber>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_publisher != null && _publisher.IsHealthy())
        {
            _logger.LogInformation("✅ RabbitMQ Publisher initialized successfully");
        }

        if (_subscriber != null && _subscriber.IsHealthy())
        {
            _logger.LogInformation("✅ RabbitMQ Subscriber initialized successfully");
        }

        if (_publisher == null && _subscriber == null)
        {
            _logger.LogWarning("⚠️ No RabbitMQ services registered");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ services shutdown");
        return Task.CompletedTask;
    }
}
