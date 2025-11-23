using Consul;

namespace Custom.Framework.Consul;

/// <summary>
/// Helper service for discovering and connecting to services registered in Consul
/// </summary>
public class ConsulServiceDiscovery
{
    private readonly IConsulClient _consulClient;

    public ConsulServiceDiscovery(IConsulClient consulClient)
    {
        _consulClient = consulClient;
    }

    /// <summary>
    /// Gets the endpoint URL for a healthy instance of the specified service
    /// </summary>
    /// <param name="serviceName">Name of the service to discover</param>
    /// <param name="tag">Optional tag to filter services</param>
    /// <returns>URL of a healthy service instance</returns>
    /// <exception cref="ServiceNotFoundException">Thrown when no healthy service is found</exception>
    public async Task<string> GetServiceEndpointAsync(string serviceName, string? tag = null)
    {
        var services = await _consulClient.Health.Service(serviceName, tag, true);
        var service = services.Response.FirstOrDefault();

        if (service == null)
        {
            throw new ServiceNotFoundException($"No healthy instance of service '{serviceName}' found");
        }

        var scheme = service.Service.Meta?.ContainsKey("scheme") == true 
            ? service.Service.Meta["scheme"] 
            : "http";

        return $"{scheme}://{service.Service.Address}:{service.Service.Port}";
    }

    /// <summary>
    /// Gets all healthy endpoints for the specified service
    /// </summary>
    /// <param name="serviceName">Name of the service to discover</param>
    /// <param name="tag">Optional tag to filter services</param>
    /// <returns>List of URLs for all healthy service instances</returns>
    public async Task<IList<string>> GetAllServiceEndpointsAsync(string serviceName, string? tag = null)
    {
        var services = await _consulClient.Health.Service(serviceName, tag, true);

        return services.Response.Select(service =>
        {
            var scheme = service.Service.Meta?.ContainsKey("scheme") == true
                ? service.Service.Meta["scheme"]
                : "http";

            return $"{scheme}://{service.Service.Address}:{service.Service.Port}";
        }).ToList();
    }

    /// <summary>
    /// Gets detailed information about all healthy instances of a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="tag">Optional tag to filter services</param>
    /// <returns>List of service entries with full details</returns>
    public async Task<IList<ServiceEntry>> GetServiceInstancesAsync(string serviceName, string? tag = null)
    {
        var result = await _consulClient.Health.Service(serviceName, tag, true);
        return result.Response.ToList();
    }

    /// <summary>
    /// Checks if a service with the given name is registered and healthy
    /// </summary>
    /// <param name="serviceName">Name of the service to check</param>
    /// <returns>True if at least one healthy instance exists</returns>
    public async Task<bool> IsServiceHealthyAsync(string serviceName)
    {
        var services = await _consulClient.Health.Service(serviceName, null, true);
        return services.Response.Any();
    }

    /// <summary>
    /// Gets a value from Consul KV store
    /// </summary>
    /// <param name="key">Key to retrieve</param>
    /// <returns>Value as string, or null if not found</returns>
    public async Task<string?> GetConfigValueAsync(string key)
    {
        var result = await _consulClient.KV.Get(key);
        
        if (result.Response == null)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(result.Response.Value);
    }

    /// <summary>
    /// Sets a value in Consul KV store
    /// </summary>
    /// <param name="key">Key to set</param>
    /// <param name="value">Value to store</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetConfigValueAsync(string key, string value)
    {
        var putResult = await _consulClient.KV.Put(new KVPair(key)
        {
            Value = System.Text.Encoding.UTF8.GetBytes(value)
        });

        return putResult.Response;
    }
}

/// <summary>
/// Exception thrown when a requested service cannot be found in Consul
/// </summary>
public class ServiceNotFoundException : Exception
{
    public ServiceNotFoundException(string message) : base(message)
    {
    }

    public ServiceNotFoundException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
