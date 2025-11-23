using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Consul;

/// <summary>
/// Extension methods for registering Consul services in DI container
/// </summary>
public static class ConsulExtensions
{
    /// <summary>
    /// Adds Consul service discovery and registration to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Consul settings</param>
    /// <param name="sectionName">Name of the configuration section (default: "Consul")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConsul(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Consul")
    {
        // Configure options from configuration
        services.Configure<ConsulOptions>(configuration.GetSection(sectionName));

        // Register Consul client
        services.AddSingleton<IConsulClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(config =>
            {
                config.Address = new Uri(options.ConsulAddress);
                if (!string.IsNullOrEmpty(options.Datacenter))
                {
                    config.Datacenter = options.Datacenter;
                }
            });
        });

        // Register automatic service registration
        services.AddHostedService<ConsulServiceRegistration>();

        return services;
    }

    /// <summary>
    /// Adds Consul service discovery and registration with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Consul options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConsul(
        this IServiceCollection services,
        Action<ConsulOptions> configureOptions)
    {
        // Configure options using action
        services.Configure(configureOptions);

        // Register Consul client
        services.AddSingleton<IConsulClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(config =>
            {
                config.Address = new Uri(options.ConsulAddress);
                if (!string.IsNullOrEmpty(options.Datacenter))
                {
                    config.Datacenter = options.Datacenter;
                }
            });
        });

        // Register automatic service registration
        services.AddHostedService<ConsulServiceRegistration>();

        return services;
    }

    /// <summary>
    /// Adds only the Consul client without automatic registration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="consulAddress">Address of the Consul server</param>
    /// <param name="datacenter">Optional datacenter name</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConsulClient(
        this IServiceCollection services,
        string consulAddress,
        string? datacenter = null)
    {
        services.AddSingleton<IConsulClient>(_ =>
        {
            return new ConsulClient(config =>
            {
                config.Address = new Uri(consulAddress);
                if (!string.IsNullOrEmpty(datacenter))
                {
                    config.Datacenter = datacenter;
                }
            });
        });

        return services;
    }
}
