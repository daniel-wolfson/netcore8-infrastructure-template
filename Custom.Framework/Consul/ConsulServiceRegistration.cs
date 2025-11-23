using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Consul;

/// <summary>
/// Hosted service that automatically registers the application with Consul on startup
/// and deregisters it on shutdown
/// </summary>
public class ConsulServiceRegistration : IHostedService
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _options;
    private readonly ILogger<ConsulServiceRegistration> _logger;
    private string? _registrationId;

    public ConsulServiceRegistration(
        IConsulClient consulClient,
        IOptions<ConsulOptions> options,
        ILogger<ConsulServiceRegistration> logger)
    {
        _consulClient = consulClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationId = $"{_options.ServiceName}-{_options.ServiceId}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _options.ServiceName,
            Address = _options.ServiceAddress,
            Port = _options.ServicePort,
            Tags = _options.Tags,
            Meta = _options.Meta,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_options.ServiceAddress}:{_options.ServicePort}{_options.HealthCheckPath}",
                Interval = _options.HealthCheckInterval,
                Timeout = _options.HealthCheckTimeout,
                DeregisterCriticalServiceAfter = _options.DeregisterCriticalServiceAfter
            }
        };

        try
        {
            await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
            _logger.LogInformation(
                "Service {ServiceId} ({ServiceName}) registered with Consul at {Address}:{Port}",
                _registrationId,
                _options.ServiceName,
                _options.ServiceAddress,
                _options.ServicePort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register service {ServiceId} with Consul", _registrationId);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registrationId != null)
        {
            try
            {
                await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
                _logger.LogInformation("Service {ServiceId} deregistered from Consul", _registrationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deregister service {ServiceId} from Consul", _registrationId);
            }
        }
    }
}
