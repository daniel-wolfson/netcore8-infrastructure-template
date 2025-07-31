using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Custom.Framework.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private static readonly ConcurrentDictionary<string, IConnectionMultiplexer> _connections = new();
        private readonly string? _redisConnectionString;
        private readonly IConnectionMultiplexer? _connectionMultiplexer;
        private readonly Func<IConnectionMultiplexer>? _connectionMultiplexerFactory;

        public RedisHealthCheck(string redisConnectionString)
        {
            _redisConnectionString = Guard.ThrowIfNull(redisConnectionString);
        }

        public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = Guard.ThrowIfNull(connectionMultiplexer);
        }

        /// <summary>
        /// Creates an instance of <seealso cref="RedisHealthCheck"/> that calls provided factory when needed for the first time.
        /// </summary>
        internal RedisHealthCheck(Func<IConnectionMultiplexer> connectionMultiplexerFactory)
        {
            _connectionMultiplexerFactory = connectionMultiplexerFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                IConnectionMultiplexer? connection = _connectionMultiplexer ?? _connectionMultiplexerFactory?.Invoke();

                if (_redisConnectionString is not null && !_connections.TryGetValue(_redisConnectionString, out connection))
                {
                    try
                    {
                        var connectionMultiplexerTask = ConnectionMultiplexer.ConnectAsync(_redisConnectionString!);
                        connection = await TimeoutAsync(connectionMultiplexerTask, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return new HealthCheckResult(context.Registration.FailureStatus, description: "Healthcheck timed out");
                    }

                    if (!_connections.TryAdd(_redisConnectionString, connection))
                    {
                        // Dispose new connection which we just created, because we don't need it.
                        connection.Dispose();
                        connection = _connections[_redisConnectionString];
                    }
                }

                foreach (var endPoint in connection!.GetEndPoints(configuredOnly: true))
                {
                    var server = connection.GetServer(endPoint);

                    if (server.ServerType != ServerType.Cluster)
                    {
                        await connection.GetDatabase().PingAsync().ConfigureAwait(false);
                        await server.PingAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        var clusterInfo = await server.ExecuteAsync("CLUSTER", "INFO").ConfigureAwait(false);

                        if (clusterInfo is object && !clusterInfo.IsNull)
                        {
                            if (!clusterInfo.ToString()!.Contains("cluster_state:ok"))
                            {
                                //cluster info is not ok!
                                return new HealthCheckResult(context.Registration.FailureStatus, description: $"INFO CLUSTER is not on OK state for endpoint {endPoint}");
                            }
                        }
                        else
                        {
                            //cluster info cannot be read for this cluster node
                            return new HealthCheckResult(context.Registration.FailureStatus, description: $"INFO CLUSTER is null or can't be read for endpoint {endPoint}");
                        }
                    }
                }

                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                if (_redisConnectionString is not null)
                {
                    _connections.TryRemove(_redisConnectionString, out var connection);
#pragma warning disable IDISP007 // Don't dispose injected [false positive here]
                    connection?.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
                }
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            }
        }

        // Remove when https://github.com/StackExchange/StackExchange.Redis/issues/1039 is done
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

    internal class Guard
    {
        /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="throwOnEmptyString">Only applicable to strings.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static T ThrowIfNull<T>([NotNull] T? argument, bool throwOnEmptyString = false, [CallerArgumentExpression("argument")] string? paramName = null)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(argument, paramName);
            if (throwOnEmptyString && argument is string s && string.IsNullOrEmpty(s))
                throw new ArgumentNullException(paramName);
            return argument;
        }
    }
}