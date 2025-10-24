using Custom.Framework.Helpers;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;  // For HealthCheckConfiguration
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using StackExchange.Redis;  // Added for Redis health check
using System.Text.Json.Serialization;
using Testcontainers.Redis;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Dapr;

/// <summary>
/// Dapr test infrastructure stack - creates a unified "dapr" compose stack in Docker Desktop
/// Similar to "monitoring" or "kafka" stacks shown in Docker Desktop
/// </summary>
public class DaprTestContainer(string appId = "testapp") : IAsyncLifetime
{
    private readonly string _appId = appId;
    private DockerClient? _dockerClient;
    private INetwork? _network;
    private RedisContainer? _redisContainer;
    private IContainer? _daprSidecar;

    public ITestOutputHelper? Output { get; set; }
    public string DaprHttpPort { get; private set; } = "3500";
    public string DaprGrpcPort { get; private set; } = "50001";
    public bool IsForceCleanup { get; set; } = true;

    public string DaprHttpEndpoint => $"http://localhost:{DaprHttpPort}";
    public string DaprGrpcEndpoint => $"http://localhost:{DaprGrpcPort}";
    public string RedisConnectionString => _redisContainer?.GetConnectionString() ?? "";

    public async Task InitializeAsync()
    {
        try
        {
            Output?.WriteLine($"🚀 Starting {_appId} stack...");

            await StartNetworkAsync(_appId);

            await StartRedisAsync();

            await StartDaprAsync();

            Output?.WriteLine($"✅ {_appId} stack is ready!");
            Output?.WriteLine("📊 Dapr Stack Endpoints:");
            Output?.WriteLine($"  Dapr HTTP: {DaprHttpEndpoint}");
            Output?.WriteLine($"  Dapr gRPC: {DaprGrpcEndpoint}");
            Output?.WriteLine($"  Redis: {RedisConnectionString}");
        }
        catch (Exception ex)
        {
            Output?.WriteLine($"❌ Starting {_appId} stack failed. Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Alternative: Start test app as a Docker container (for full Dapr integration with pub/sub)
    /// </summary>
    public async Task<IContainer> StartAppContainerAsync(string appImageName = "mcr.microsoft.com/dotnet/aspnet:8.0")
    {
        Output?.WriteLine("⏳ Starting app container...");

        var appContainer = new ContainerBuilder()
            .WithImage(appImageName)
            .WithName($"{_appId}-app")
            .WithNetwork(_network)
            .WithNetworkAliases("app")
            .WithPortBinding(5000, 5000)
            .WithEnvironment("ASPNETCORE_URLS", "http://+:5000")
            .WithEnvironment("Dapr__HttpEndpoint", "http://dapr:3500")
            .WithEnvironment("Dapr__GrpcEndpoint", "http://dapr:50001")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(5000)
                    .ForPath("/health")))
            .Build();

        await appContainer.StartAsync();

        Output?.WriteLine("✅ App container ready on port 5000");
        return appContainer;
    }

    // Update Dapr to connect to app container
    public void ConfigureDaprForAppContainer()
    {
        Output?.WriteLine("ℹ️  To use app container with Dapr:");
        Output?.WriteLine("ℹ️  1. Call StartAppContainerAsync() to create app container");
        Output?.WriteLine("ℹ️  2. Rebuild DaprTestContainer with --app-port 5000");
        Output?.WriteLine("ℹ️  3. Dapr will discover subscriptions from http://app:5000/dapr/subscribe");
    }

    private async Task StartNetworkAsync(string appId)
    {
        var networkName = $"{appId}-network";
        _dockerClient = new DockerClientConfiguration().CreateClient();

        var networks = (await _dockerClient.Networks
                .ListNetworksAsync(new NetworksListParameters()))
                .Select(x => new NetworkResponseExt(x, _dockerClient));

        _network = networks.FirstOrDefault(n => n.Name == networkName);

        if (_network == null)
        {
            _network = new NetworkBuilder()
                .WithName(networkName)
                .Build();

            await _network.CreateAsync();
            Output?.WriteLine($"✅ Created network: {_network.Name}");
        }
        else
        {
            Output?.WriteLine($"✅ Used exist network: {_network.Name}");
        }
    }

    private async Task StartRedisAsync()
    {
        try
        {
            Output?.WriteLine("⏳ Starting Redis...");

            _redisContainer = new RedisBuilder()
                .WithImage("redis:7.0")
                .WithName($"{_appId}-redis")
                .WithNetwork(_network)
                .WithNetworkAliases("redis")
                .WithAutoRemove(true)
                .WithPortBinding(6379, 6379)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilMessageIsLogged("Ready to accept connections")
                .UntilCommandIsCompleted("redis-cli", "ping"))
                .Build();

            await _redisContainer.StartAsync();

            await Task.Delay(300);

            await VerifyRedisHealthAsync(_redisContainer!.GetConnectionString());

            Output?.WriteLine($"✅ Redis ready on port 6379");
        }
        catch (Exception ex)
        {
            Output?.WriteLine($"❌ Redis startup failed: {ex.Message}");
            throw;
        }
    }

    private async Task VerifyRedisHealthAsync(string connectionString)
    {
        try
        {
            // Verify Redis connectivity using StackExchange.Redis
            using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);

            var db = connection.GetDatabase();
            var pingResult = await db.PingAsync();

            if (pingResult.TotalMilliseconds > 5000)
            {
                throw new Exception($"Redis ping took too long: {pingResult.TotalMilliseconds}ms");
            }

            Output?.WriteLine($"✅ Redis health verified (ping: {pingResult.TotalMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            throw new Exception($"Redis health check failed: {ex.Message}", ex);
        }
    }

    private async Task StartDaprAsync()
    {
        Output?.WriteLine("⏳ Starting Dapr sidecar...");

        // Create Dapr components configuration
        var componentsPath = await CreateDaprComponentsAsync();

        var daprHttpPort = 3500;
        var daprGrpcPort = 50001;
        var appPort = 5000;

        DaprHttpPort = daprHttpPort.ToString();
        DaprGrpcPort = daprGrpcPort.ToString();

        _daprSidecar = new ContainerBuilder()
            .WithImage("daprio/daprd")
            .WithName($"{_appId}-sidecar")
            .WithNetwork(_network)
            .WithNetworkAliases("dapr")
            .WithEntrypoint("/daprd")
            .WithAutoRemove(true)
            .WithCommand(
                "--app-id", _appId,
                "--app-port", appPort.ToString(),  // ✅ Dapr will connect to app
                "--app-protocol", "http",
                "--app-address", "host.docker.internal",  // ✅ Tell Dapr where app is
                "--dapr-http-port", "3500",
                "--dapr-grpc-port", "50001",
                "--components-path", "/components",
                "--log-level", "debug",
                "--enable-app-health-check", "false")  // ✅ Disable health check
            .WithPortBinding(daprHttpPort, 3500)
            .WithPortBinding(daprGrpcPort, 50001)
            .WithBindMount(componentsPath, "/components")
            .WithExtraHost("host.docker.internal", "host-gateway")  // ✅ Map to host machine IP
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                .ForPort(3500)
                .ForPath("/v1.0/healthz")
                //.ForStatusCode(System.Net.HttpStatusCode.NoContent)
            ))
            .Build();

        try
        {
            await _daprSidecar.StartAsync();
        }
        catch (Exception)
        {
            throw;
        }

        Output?.WriteLine($"✅ Dapr sidecar ready - HTTP: {DaprHttpEndpoint}, gRPC: {DaprGrpcEndpoint}");
        Output?.WriteLine($"✅ Dapr configured to reach app at host.docker.internal:{appPort}");
        Output?.WriteLine($"ℹ️  Dapr will discover subscriptions from http://host.docker.internal:{appPort}/dapr/subscribe");
    }

    private async Task<string> CreateDaprComponentsAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // state store (Redis)
        var stateStoreYaml = @"apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
  - name: redisPassword
    value: """"
  - name: actorStateStore
    value: ""true""
";
        await File.WriteAllTextAsync(Path.Combine(tempDir, "statestore.yaml"), stateStoreYaml);

        // pub/sub (Redis)
        var pubsubYaml = @"apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
  - name: redisPassword
    value: """"
";

        await File.WriteAllTextAsync(Path.Combine(tempDir, "pubsub.yaml"), pubsubYaml);

        Output?.WriteLine($"✅ Dapr components created at: {tempDir}");
        Output?.WriteLine($"   📄 statestore.yaml");
        Output?.WriteLine($"   📄 pubsub.yaml");

        // Verify files were created and log their content for debugging
        if (!File.Exists(Path.Combine(tempDir, "statestore.yaml")))
        {
            throw new FileNotFoundException("📄 statestore.yaml not found");
        }

        if (!File.Exists(Path.Combine(tempDir, "pubsub.yaml")))
        {
            throw new FileNotFoundException("📄 statestore.yaml not found");
        }

        return tempDir;
    }

    public async Task DisposeAsync()
    {
        Output?.WriteLine($"🛑 Stopping {_appId} stack...");

        var disposeTasks = new List<Task>();

        if (_daprSidecar != null)
            disposeTasks.Add(_daprSidecar.DisposeAsync().AsTask());
        if (_redisContainer != null)
            disposeTasks.Add(_redisContainer.DisposeAsync().AsTask());

        await Task.WhenAll(disposeTasks);

        if (_network != null)
        {
            await _network.DeleteAsync();
            Output?.WriteLine("✅ Network removed");
        }

        // Dispose DockerClient last, after all operations are complete
        _dockerClient?.Dispose();

        Output?.WriteLine($"✅ {_appId} stack stopped");
    }

    /// <summary>
    /// Force cleanup networks matching the pattern (stops containers and removes networks)
    /// </summary>
    public async Task ForceCleanupNetworksAsync()
    {
        using var client = new DockerClientConfiguration().CreateClient();

        try
        {
            // Find all networks matching the pattern
            var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [_appId] = true }
                }
            });

            foreach (var network in networks)
            {
                Output?.WriteLine($"🔍 Found network: {network.Name}");

                // Get network details including connected containers
                var networkDetails = await client.Networks.InspectNetworkAsync(network.ID);

                if (networkDetails.Containers != null && networkDetails.Containers.Any())
                {
                    Output?.WriteLine($"⚠️  Network {network.Name} has {networkDetails.Containers.Count} container(s) attached");

                    // Stop and remove all containers attached to this network
                    foreach (var container in networkDetails.Containers)
                    {
                        try
                        {
                            Output?.WriteLine($"  🛑 Stopping container: {container.Value.Name}");
                            await client.Containers.StopContainerAsync(
                                container.Key,
                                new ContainerStopParameters { WaitBeforeKillSeconds = 5 }
                            );

                            Output?.WriteLine($"  🗑️  Removing container: {container.Value.Name}");
                            await client.Containers.RemoveContainerAsync(
                                container.Key,
                                new ContainerRemoveParameters { Force = true }
                            );
                        }
                        catch (Exception ex)
                        {
                            Output?.WriteLine($"  ⚠️  Failed to stop/remove container {container.Value.Name}: {ex.Message}");
                        }
                    }
                }

                // Remove the network
                try
                {
                    await client.Networks.DeleteNetworkAsync(network.ID);
                    Output?.WriteLine($"✅ Removed network: {network.Name}");
                }
                catch (Exception ex)
                {
                    Output?.WriteLine($"❌ Failed to remove network {network.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Output?.WriteLine($"❌ Error during force cleanup: {ex.Message}");
            throw;
        }
    }

    public class NetworkResponseExt(NetworkResponse networkResponse, DockerClient client) : INetwork
    {
        private readonly DockerClient _client = client;
        private bool _disposed = false;

        [JsonPropertyName("Name")]
        public string Name { get; set; } = networkResponse.Name;

        [JsonPropertyName("Id")]
        public string ID { get; set; } = networkResponse.ID;

        [JsonPropertyName("Created")]
        public DateTime Created { get; set; } = networkResponse.Created;

        [JsonPropertyName("Scope")]
        public string Scope { get; set; } = networkResponse.Scope;

        [JsonPropertyName("Driver")]
        public string Driver { get; set; } = networkResponse.Driver;

        [JsonPropertyName("EnableIPv4")]
        public bool EnableIPv4 { get; set; } = networkResponse.EnableIPv4;

        [JsonPropertyName("EnableIPv6")]
        public bool EnableIPv6 { get; set; } = networkResponse.EnableIPv6;

        [JsonPropertyName("IPAM")]
        public IPAM IPAM { get; set; } = networkResponse.IPAM;

        [JsonPropertyName("Internal")]
        public bool Internal { get; set; } = networkResponse.Internal;

        [JsonPropertyName("Attachable")]
        public bool Attachable { get; set; } = networkResponse.Attachable;

        [JsonPropertyName("Ingress")]
        public bool Ingress { get; set; } = networkResponse.Ingress;

        [JsonPropertyName("ConfigFrom")]
        public ConfigReference ConfigFrom { get; set; } = networkResponse.ConfigFrom;

        [JsonPropertyName("ConfigOnly")]
        public bool ConfigOnly { get; set; } = networkResponse.ConfigOnly;

        [JsonPropertyName("Containers")]
        public IDictionary<string, EndpointResource> Containers { get; set; } = networkResponse.Containers;

        [JsonPropertyName("Options")]
        public IDictionary<string, string> Options { get; set; } = networkResponse.Options;

        [JsonPropertyName("Labels")]
        public IDictionary<string, string> Labels { get; set; } = networkResponse.Labels;

        [JsonPropertyName("Peers")]
        public IList<PeerInfo> Peers { get; set; } = networkResponse.Peers;

        [JsonPropertyName("Services")]
        public IDictionary<string, ServiceInfo> Services { get; set; } = networkResponse.Services;

        string INetwork.Name => Name;

        Task IFutureResource.CreateAsync(CancellationToken ct)
        {
            // Network already exists, so do nothing
            return Task.CompletedTask;
        }

        Task IFutureResource.DeleteAsync(CancellationToken ct)
        {
            if (!_disposed)
            {
                _disposed = true;
                return _client.Networks.DeleteNetworkAsync(ID, ct);
            }
            return Task.CompletedTask;
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                return new ValueTask(_client.Networks.DeleteNetworkAsync(ID));
            }
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
