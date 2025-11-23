using Custom.Framework.Tests.Docker;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Elastic;

/// <summary>
/// Testcontainers-based Elasticsearch test infrastructure
/// Provides Elasticsearch and Kibana for integration testing
/// </summary>
public class ElasticTestContainer : IAsyncLifetime
{
    private readonly ITestOutputHelper? _output;
    private IContainer? _elasticContainer;
    private IContainer? _kibanaContainer;
    private INetwork? _network;
    private DockerClient? _dockerClient;

    public string ElasticUrl { get; private set; } = string.Empty;
    public string KibanaUrl { get; private set; } = string.Empty;
    public string Username { get; private set; } = "elastic";
    public string Password { get; private set; } = "changeme";

    public ElasticTestContainer(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _output?.WriteLine("🚀 Starting Elasticsearch test infrastructure...");

            await StartNetworkAsync();
            await StartElasticsearchAsync();
            await StartKibanaAsync();

            _output?.WriteLine("✅ Elasticsearch stack is ready!");
            _output?.WriteLine($"📊 Elasticsearch: {ElasticUrl}");
            _output?.WriteLine($"📊 Kibana: {KibanaUrl}");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"❌ Failed to start Elasticsearch: {ex.Message}");
            throw;
        }
    }

    private async Task StartNetworkAsync()
    {
        var networkName = "elastic-test-network";
        _dockerClient = new DockerClientConfiguration(
            new Uri("npipe://./pipe/docker_engine")).CreateClient();
        _dockerClient.DefaultTimeout = TimeSpan.FromSeconds(300);

        var network = await DockerNetworkManager.GetNetworkAsync(networkName);
        if (network != null)
        {
            Console.WriteLine($"Network ID: {network.ID}");
            Console.WriteLine($"Network Name: {network.Name}");
        }
        else
        {
            _network = new NetworkBuilder()
                .WithName(networkName)
                .Build();

            await _network.CreateAsync();
            _output?.WriteLine($"✅ Created network: {networkName}");
        }
    }

    private async Task StartElasticsearchAsync()
    {
        _output?.WriteLine("⏳ Starting Elasticsearch...");

        _elasticContainer = new ContainerBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.12.0")
            .WithNetwork(_network)
            .WithNetworkAliases("elasticsearch")
            .WithPortBinding(9200, true)
            .WithPortBinding(9300, true)
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("xpack.security.http.ssl.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(9200)
                    .ForPath("/_cluster/health")
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        await _elasticContainer.StartAsync();
        await Task.Delay(2000); // Additional wait for stability

        // Get the dynamically assigned port
        var elasticPort = _elasticContainer.GetMappedPublicPort(9200);
        ElasticUrl = $"http://localhost:{elasticPort}";

        await VerifyElasticsearchHealthAsync();

        _output?.WriteLine($"✅ Elasticsearch ready at {ElasticUrl}");
    }

    private async Task StartKibanaAsync()
    {
        _output?.WriteLine("⏳ Starting Kibana...");

        _kibanaContainer = new ContainerBuilder()
            .WithImage("docker.elastic.co/kibana/kibana:8.12.0")
            .WithNetwork(_network)
            .WithPortBinding(5601, true)
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(5601)
                    .ForPath("/api/status")
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        await _kibanaContainer.StartAsync();

        // Get the dynamically assigned port
        var kibanaPort = _kibanaContainer.GetMappedPublicPort(5601);
        KibanaUrl = $"http://localhost:{kibanaPort}";

        _output?.WriteLine($"✅ Kibana ready at {KibanaUrl}");
    }

    private async Task VerifyElasticsearchHealthAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{ElasticUrl}/_cluster/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        _output?.WriteLine($"✅ Cluster health: {content}");
    }

    public async Task DisposeAsync()
    {
        _output?.WriteLine("🛑 Stopping Elasticsearch test infrastructure...");

        if (_kibanaContainer != null)
            await _kibanaContainer.DisposeAsync();

        if (_elasticContainer != null)
            await _elasticContainer.DisposeAsync();

        if (_network != null)
            await _network.DeleteAsync();

        _dockerClient?.Dispose();

        _output?.WriteLine("✅ Elasticsearch stack stopped");
    }
}
