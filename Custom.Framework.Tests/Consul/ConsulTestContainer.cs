using Consul;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Consul;

/// <summary>
/// Consul test infrastructure - creates Consul server for integration tests
/// Similar to DaprTestContainer pattern
/// </summary>
public class ConsulTestContainer(string datacenter = "dc1") : IAsyncLifetime
{
    private readonly string _datacenter = datacenter;
    private DockerClient? _dockerClient;
    private INetwork? _network;
    private IContainer? _consulServer;

    public ITestOutputHelper? Output { get; set; }
    public string HttpPort { get; private set; } = "8500";
    public string DnsPort { get; private set; } = "8600";
    public bool IsForceCleanup { get; set; } = true;

    public string HttpEndpoint => $"http://localhost:{HttpPort}";
    public string WebUIEndpoint => $"{HttpEndpoint}/ui";

    public async Task InitializeAsync()
    {
        try
        {
            Output?.WriteLine($"🚀 Starting Consul datacenter '{_datacenter}'...");

            await StartNetworkAsync();
            await StartConsulServerAsync();

            Output?.WriteLine($"✅ Consul is ready!");
            Output?.WriteLine($"📊 Consul Endpoints:");
            Output?.WriteLine($"  HTTP API: {HttpEndpoint}");
            Output?.WriteLine($"  Web UI: {WebUIEndpoint}");
        }
        catch (Exception ex)
        {
            Output?.WriteLine($"❌ Starting Consul failed. Error: {ex.Message}");
            throw;
        }
    }

    private async Task StartNetworkAsync()
    {
        var networkName = $"consul-{_datacenter}-network";
        
        // Create Docker client with extended timeout to handle slow Docker Desktop responses
        var dockerClientConfig = new DockerClientConfiguration(
            new Uri("npipe://./pipe/docker_engine"));
        _dockerClient = dockerClientConfig.CreateClient();
        _dockerClient.DefaultTimeout = TimeSpan.FromSeconds(300); // 5 minutes timeout

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
            Output?.WriteLine($"✅ Using existing network: {_network.Name}");
        }
    }

    private async Task StartConsulServerAsync()
    {
        Output?.WriteLine("⏳ Starting Consul server...");

        var httpPort = 8500;
        var dnsPort = 8600;

        HttpPort = httpPort.ToString();
        DnsPort = dnsPort.ToString();

        var containerName = $"consul-{_datacenter}-server";

        // Check if container already exists
        if (_dockerClient != null)
        {
            try
            {
                // List ALL containers to see what's using port 8500
                var allContainers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true
                });

                Output?.WriteLine($"🔍 Checking all containers for port conflicts...");
                foreach (var container in allContainers)
                {
                    var ports = container.Ports?.Where(p => p.PublicPort == 8500 || p.PublicPort == 8600).ToList();
                    if (ports?.Any() == true)
                    {
                        Output?.WriteLine($"⚠️  Container using ports 8500/8600: {container.Names.FirstOrDefault()} (ID: {container.ID[..12]}, State: {container.State})");
                        
                        // Stop if running
                        if (container.State == "running")
                        {
                            Output?.WriteLine($"  🛑 Stopping container...");
                            await _dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
                        }

                        // Remove container
                        Output?.WriteLine($"  🗑️  Removing container...");
                        await _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters { Force = true });
                        Output?.WriteLine($"✅ Cleaned up container using Consul ports");
                    }
                }
                
                // Also check specifically for our named container
                var existingContainers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [containerName] = true }
                    }
                });

                foreach (var container in existingContainers)
                {
                    Output?.WriteLine($"⚠️  Found existing container by name: {container.Names.FirstOrDefault()}");

                    // Stop if running
                    if (container.State == "running")
                    {
                        Output?.WriteLine($"  🛑 Stopping container...");
                        await _dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
                    }

                    // Remove container
                    Output?.WriteLine($"  🗑️  Removing container...");
                    await _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters { Force = true });
                    Output?.WriteLine($"✅ Cleaned up existing container");
                }
            }
            catch (Exception ex)
            {
                Output?.WriteLine($"⚠️  Error checking for existing containers: {ex.Message}");
            }
        }

        _consulServer = new ContainerBuilder()
            .WithImage("hashicorp/consul:1.20")
            .WithName(containerName)
            .WithNetwork(_network)
            .WithNetworkAliases("consul-server")
            .WithCommand("agent", "-server", "-ui",
                "-bootstrap-expect=1",
                "-client=0.0.0.0",
                $"-datacenter={_datacenter}",
                "-log-level=INFO")
            .WithPortBinding(httpPort, 8500)
            .WithPortBinding(dnsPort, 8600)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8500)
                    .ForPath("/v1/status/leader")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _consulServer.StartAsync();

        // Verify Consul is ready
        await VerifyConsulHealthAsync();

        Output?.WriteLine($"✅ Consul server ready - HTTP: {HttpEndpoint}");
    }

    private async Task StartConsulServerAsync2()
    {
        Output?.WriteLine("⏳ Starting Consul server...");

        var httpPort = 8500;
        var dnsPort = 8600;

        HttpPort = httpPort.ToString();
        DnsPort = dnsPort.ToString();

        _consulServer = new ContainerBuilder()
            .WithImage("hashicorp/consul:1.22")
            .WithName($"consul-{_datacenter}-server")
            .WithNetwork(_network)
            .WithNetworkAliases("consul-server")
            .WithCommand("agent", "-server", "-ui",
                "-bootstrap-expect=1",
                "-client=0.0.0.0",
                $"-datacenter={_datacenter}",
                "-log-level=INFO")
            .WithPortBinding(httpPort, 8500)
            .WithPortBinding(dnsPort, 8600)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8500)
                    .ForPath("/v1/status/leader")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _consulServer.StartAsync();

        // Verify Consul is ready
        await VerifyConsulHealthAsync();

        Output?.WriteLine($"✅ Consul server ready - HTTP: {HttpEndpoint}");
    }

    private async Task VerifyConsulHealthAsync()
    {
        try
        {
            using var client = new ConsulClient(config =>
            {
                config.Address = new Uri(HttpEndpoint);
            });

            var leader = await client.Status.Leader();
            Output?.WriteLine($"✅ Consul leader elected: {leader}");

            var members = await client.Agent.Members(false);
            Output?.WriteLine($"✅ Consul cluster members: {members.Response.Length}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Consul health check failed: {ex.Message}", ex);
        }
    }

    public async Task<IConsulClient> CreateClientAsync()
    {
        var client = new ConsulClient(config =>
        {
            config.Address = new Uri(HttpEndpoint);
            config.Datacenter = _datacenter;
        });

        return await Task.FromResult(client);
    }

    public async Task DisposeAsync()
    {
        Output?.WriteLine($"🛑 Stopping Consul datacenter '{_datacenter}'...");

        if (_consulServer != null)
            await _consulServer.DisposeAsync();

        if (_network != null)
        {
            await _network.DeleteAsync();
            Output?.WriteLine("✅ Network removed");
        }

        _dockerClient?.Dispose();
        Output?.WriteLine($"✅ Consul stopped");
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
                    ["name"] = new Dictionary<string, bool> { [$"consul-{_datacenter}"] = true }
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

    /// <summary>
    /// NetworkResponseExt helper class (same pattern as DaprTestContainer)
    /// </summary>
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
        public IDictionary<string, Docker.DotNet.Models.ServiceInfo> Services { get; set; } = networkResponse.Services;

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
