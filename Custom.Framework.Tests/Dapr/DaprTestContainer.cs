using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Redis;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Dapr;

/// <summary>
/// Test infrastructure for Dapr integration testing
/// Manages Dapr sidecar and required dependencies (Redis, etc.)
/// </summary>
public class DaprTestContainer : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private INetwork? _network;
    private RedisContainer? _redisContainer;
    private IContainer? _daprSidecar;

    public string DaprHttpPort { get; private set; } = "3500";
    public string DaprGrpcPort { get; private set; } = "50001";
    public string AppId { get; private set; } = "testapp";

    public string DaprHttpEndpoint => $"http://localhost:{DaprHttpPort}";
    public string DaprGrpcEndpoint => $"http://localhost:{DaprGrpcPort}";

    public DaprTestContainer(ITestOutputHelper output, string appId = "testapp")
    {
        _output = output;
        AppId = appId;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 Starting Dapr test infrastructure...");

        // Create Docker network
        _network = new NetworkBuilder()
            .WithName($"dapr-test-network-{Guid.NewGuid():N}")
            .Build();

        await _network.CreateAsync();
        _output.WriteLine($"✅ Created Docker network: {_network.Name}");

        // Start Redis (required for Dapr state store)
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7.0")
            .WithNetwork(_network)
            .WithNetworkAliases("redis")
            .WithPortBinding(6379, true)
            .Build();

        await _redisContainer.StartAsync();
        _output.WriteLine($"✅ Redis started on port {_redisContainer.GetMappedPublicPort(6379)}");

        // Create Dapr components configuration
        var componentsPath = await CreateDaprComponentsAsync();

        // Start Dapr sidecar
        var daprHttpPort = Random.Shared.Next(3500, 3600);
        var daprGrpcPort = Random.Shared.Next(50001, 50100);

        DaprHttpPort = daprHttpPort.ToString();
        DaprGrpcPort = daprGrpcPort.ToString();

        _daprSidecar = new ContainerBuilder()
            .WithImage("daprio/dapr:1.14.4")
            .WithNetwork(_network)
            .WithEntrypoint("/daprd")
            .WithCommand(
                "--app-id", AppId,
                "--app-port", "5000",
                "--dapr-http-port", "3500",
                "--dapr-grpc-port", "50001",
                "--components-path", "/components",
                "--log-level", "debug"
            )
            .WithPortBinding(daprHttpPort, 3500)
            .WithPortBinding(daprGrpcPort, 50001)
            .WithBindMount(componentsPath, "/components")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("dapr initialized"))
            .Build();

        await _daprSidecar.StartAsync();

        _output.WriteLine($"✅ Dapr sidecar started:");
        _output.WriteLine($"   HTTP endpoint: {DaprHttpEndpoint}");
        _output.WriteLine($"   gRPC endpoint: {DaprGrpcEndpoint}");
        _output.WriteLine($"   App ID: {AppId}");
    }

    private async Task<string> CreateDaprComponentsAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Create state store component (Redis)
        var stateStoreYaml = @"
            apiVersion: dapr.io/v1alpha1
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

        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "statestore.yaml"),
            stateStoreYaml);

        // Create pub/sub component (Redis)
        var pubsubYaml = @"
            apiVersion: dapr.io/v1alpha1
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

        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "pubsub.yaml"),
            pubsubYaml);

        _output.WriteLine($"✅ Dapr components created at: {tempDir}");
        return tempDir;
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("🛑 Stopping Dapr test infrastructure...");

        if (_daprSidecar != null)
        {
            await _daprSidecar.DisposeAsync();
            _output.WriteLine("✅ Dapr sidecar stopped");
        }

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
            _output.WriteLine("✅ Redis stopped");
        }

        if (_network != null)
        {
            await _network.DeleteAsync();
            _output.WriteLine("✅ Docker network removed");
        }
    }
}
