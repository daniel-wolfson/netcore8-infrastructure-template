using Custom.Framework.Configuration;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace Custom.Framework.HealthChecks
{
    public class RedisApiHealthCheck(IOptions<ApiSettings> appSettingsOptions, ILogger logger,
        IConnectionMultiplexer connection) : IHealthCheck
    {
        private readonly ApiSettings _appSettings = appSettingsOptions.Value;
        private readonly ILogger _logger = logger;
        private IConnectionMultiplexer _connection = connection;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>() {
                { "ConnectionString", _appSettings.Redis?.ConnectionString ?? "not defined"},
                { "Assembly", typeof(IConnectionMultiplexer).Assembly.FullName ?? "not defined" }
            };

            try
            {
                var _redisConnectionString = _appSettings.Redis?.ConnectionString;

                if (_redisConnectionString is not null && _connection is not null)
                {
                    try
                    {
                        var connectionMultiplexerTask = ConnectionMultiplexer.ConnectAsync(_redisConnectionString!);
                        _connection = await TimeoutAsync(connectionMultiplexerTask, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return new HealthCheckResult(HealthStatus.Unhealthy, description: "Healthcheck timed out");
                    }
                }

                foreach (var endPoint in _connection!.GetEndPoints(configuredOnly: true))
                {
                    var server = _connection.GetServer(endPoint);

                    if (server.ServerType != ServerType.Cluster)
                    {
                        await _connection.GetDatabase().PingAsync().ConfigureAwait(false);
                        await server.PingAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        var clusterInfo = await server.ExecuteAsync("CLUSTER", "INFO").ConfigureAwait(false);

                        if (clusterInfo is object && !clusterInfo.IsNull)
                        {
                            if (!clusterInfo.ToString()!.Contains("cluster_state:ok"))
                            {

                                _logger.Error("{TITLE} EndPoint failed: {MSG}",
                                    ApiHelper.LogTitle(), $"state failed for endpoint {endPoint}");

                                return new HealthCheckResult(HealthStatus.Unhealthy,
                                    description: $"Unhealthy: state failed for endpoint {endPoint}");
                            }
                        }
                        else
                        {
                            _logger.Error("{TITLE} EndPoint failed: {MSG}",
                                    ApiHelper.LogTitle(), $"can't be read for redis endpoint {endPoint}");

                            return new HealthCheckResult(HealthStatus.Unhealthy,
                                description: $"Unhealthy: can't be read for redis endpoint {endPoint}");
                        }
                    }
                }

                return new HealthCheckResult(HealthStatus.Healthy,
                    description: $"state is healthy",
                    data: data.AsReadOnly());
            }
            catch (Exception ex)
            {
                if (_connection != null)
                    _connection?.Dispose();

                //return new HealthCheckResult(HealthStatus.Unhealthy, exception: ex);
                data.Add("ErrorInfo", ex.InnerException?.Message ?? ex.Message);
                data.Add("StackTrace", ex.StackTrace ?? "");

                _logger.Error("{TITLE} Redis failed: {EX}", ApiHelper.LogTitle(), ex);

                return new HealthCheckResult(status: HealthStatus.Unhealthy,
                    description: "Redis is down.",
                    data: data.AsReadOnly());
            }
        }

        private static async Task<ConnectionMultiplexer> TimeoutAsync(Task<ConnectionMultiplexer> task, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completedTask = await Task.
                WhenAny(task, Task.Delay(Timeout.Infinite, timeoutCts.Token))
                .ConfigureAwait(false);

            if (completedTask == task)
            {
                timeoutCts.Cancel();
                return await task.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException();
        }
    }
}