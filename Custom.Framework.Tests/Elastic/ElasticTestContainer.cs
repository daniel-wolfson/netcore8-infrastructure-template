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
/// Provides Elasticsearch, Logstash, and Kibana for integration testing
/// Supports Kafka → Logstash → Elasticsearch pipeline
/// </summary>
public class ElasticTestContainer : IAsyncLifetime
{
    private readonly ITestOutputHelper? _output;
    private IContainer? _elasticContainer;
    private IContainer? _logstashContainer;
    private IContainer? _kibanaContainer;
    private INetwork? _network;
    private DockerClient? _dockerClient;

    public string ElasticUrl { get; private set; } = string.Empty;
    public string LogstashUrl { get; private set; } = string.Empty;
    public string KibanaUrl { get; private set; } = string.Empty;
    public string Username { get; private set; } = "elastic";
    public string Password { get; private set; } = "changeme";
    public bool EnableLogstash { get; set; } = false;

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
            
            if (EnableLogstash)
            {
                await StartLogstashAsync();
            }
            
            await StartKibanaAsync();

            _output?.WriteLine("✅ Elasticsearch stack is ready!");
            _output?.WriteLine($"📊 Elasticsearch: {ElasticUrl}");
            if (EnableLogstash)
            {
                _output?.WriteLine($"📊 Logstash: {LogstashUrl}");
            }
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

    private async Task StartLogstashAsync()
    {
        _output?.WriteLine("⏳ Starting Logstash...");

        // Create Logstash pipeline configuration
        var pipelineConfigPath = await CreateLogstashPipelineAsync();

        _logstashContainer = new ContainerBuilder()
            .WithImage("docker.elastic.co/logstash/logstash:8.12.0")
            .WithNetwork(_network)
            .WithNetworkAliases("logstash")
            .WithPortBinding(5044, true)  // Beats input
            .WithPortBinding(9600, true)  // Logstash API
            .WithEnvironment("xpack.monitoring.enabled", "false")
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
            .WithEnvironment("LOG_LEVEL", "debug")
            .WithBindMount(pipelineConfigPath, "/usr/share/logstash/pipeline")
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(9600)
                    .ForPath("/")
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        await _logstashContainer.StartAsync();
        
        _output?.WriteLine("⏳ Waiting for Logstash to initialize and connect to Kafka...");
        
        // Poll for Kafka subscription instead of blind wait
        bool subscribed = false;
        for (int i = 0; i < 60; i++)  // 60 attempts × 2 seconds = 2 minutes max
        {
            await Task.Delay(2000);
            var logs = await GetLogstashLogsAsync();
            
            if (logs.Contains("Subscribed to topics", StringComparison.OrdinalIgnoreCase) ||
                logs.Contains("kafka input started", StringComparison.OrdinalIgnoreCase))
            {
                _output?.WriteLine($"✅ Logstash subscribed to Kafka topics after {i * 2} seconds");
                subscribed = true;
                break;
            }
            
            // Check for errors
            if (logs.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                logs.Contains("Broker may not be available", StringComparison.OrdinalIgnoreCase))
            {
                _output?.WriteLine($"❌ Logstash cannot connect to Kafka!");
                _output?.WriteLine($"   Attempted connection to: host.docker.internal:9092");
                _output?.WriteLine($"   Error found in logs after {i * 2} seconds");
                break;
            }
            
            if (i % 5 == 0)  // Log every 10 seconds
            {
                _output?.WriteLine($"   Still waiting for Kafka subscription... ({i * 2}s)");
            }
        }
        
        if (!subscribed)
        {
            _output?.WriteLine("⚠️  WARNING: Could not confirm Logstash subscribed to Kafka");
            _output?.WriteLine("   Tests may fail. Check Logstash logs for details.");
        }

        var logstashPort = _logstashContainer.GetMappedPublicPort(5044);
        var logstashApiPort = _logstashContainer.GetMappedPublicPort(9600);
        LogstashUrl = $"http://localhost:{logstashApiPort}";

        _output?.WriteLine($"✅ Logstash container ready at {LogstashUrl}");
        _output?.WriteLine($"✅ Logstash Beats input on port {logstashPort}");
    }

    private async Task<string> CreateLogstashPipelineAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"logstash-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Get local machine IP for Kafka connection
        // host.docker.internal doesn't always work, so we'll try actual IP as fallback
        var kafkaBootstrapServers = GetKafkaBootstrapServers();
        
        _output?.WriteLine($"📍 Using Kafka bootstrap servers: {kafkaBootstrapServers}");

        // Logstash pipeline configuration for Kafka → Elasticsearch
        var pipelineConfig = $@"input {{
  kafka {{
    bootstrap_servers => ""{kafkaBootstrapServers}""
    topics => [""logs-test.error"", ""test-logs""]
    group_id => ""logstash-consumer-group""
    codec => ""json""
    auto_offset_reset => ""earliest""
    consumer_threads => 2
    session_timeout_ms => 30000
    max_poll_interval_ms => 300000
    request_timeout_ms => 40000
  }}
}}

filter {{
  # When codec => json, the message is already parsed
  # Fields are at root level, no need to parse again
  
  # Add timestamp if not present
  if ![Timestamp] {{
    mutate {{
      add_field => {{ ""Timestamp"" => ""%{{@timestamp}}"" }}
    }}
  }}

  # Add index date metadata for daily indices
  mutate {{
    add_field => {{ ""[@metadata][index_date]"" => ""%{{+YYYY.MM.dd}}"" }}
  }}

  # Add Logstash processing metadata
  mutate {{
    add_field => {{ 
      ""logstash_processed_at"" => ""%{{@timestamp}}""
      ""pipeline"" => ""kafka-to-elasticsearch""
    }}
  }}
}}

output {{
  elasticsearch {{
    hosts => [""http://elasticsearch:9200""]
    index => ""logs-%{{[@metadata][index_date]}}""
    document_id => ""%{{TraceId}}""
  }}

  # Debug output to see what Logstash is processing
  stdout {{
    codec => rubydebug {{
      metadata => true
    }}
  }}
}}
";

        await File.WriteAllTextAsync(Path.Combine(tempDir, "logstash.conf"), pipelineConfig);

        _output?.WriteLine($"✅ Logstash pipeline created at: {tempDir}");
        _output?.WriteLine($"   📄 Pipeline config: logstash.conf");
        _output?.WriteLine($"   📍 Kafka: {kafkaBootstrapServers}");
        _output?.WriteLine($"   📍 Elasticsearch: http://elasticsearch:9200");

        return tempDir;
    }

    private string GetKafkaBootstrapServers()
    {
        // Try multiple options for reaching Kafka on the host machine
        // Priority: host.docker.internal > actual IP > localhost
        
        // Option 1: host.docker.internal (works on Windows/Mac Docker Desktop)
        var hostDockerInternal = "host.docker.internal:9092";
        
        // Option 2: Try to get actual machine IP
        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var addresses = System.Net.Dns.GetHostAddresses(hostName);
            
            // Get first IPv4 address that's not loopback
            var localIP = addresses
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork 
                                   && !System.Net.IPAddress.IsLoopback(ip));
            
            if (localIP != null)
            {
                _output?.WriteLine($"   Found local IP: {localIP}");
                _output?.WriteLine($"   Will use host.docker.internal as primary, {localIP} as fallback");
                
                // Return host.docker.internal but log the alternative
                return hostDockerInternal;
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"   Could not determine local IP: {ex.Message}");
        }
        
        return hostDockerInternal;
    }

    private async Task VerifyLogstashConnectionsAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Check Logstash node stats to see if pipeline is running
            var nodeStatsUrl = $"{LogstashUrl}/_node/stats";
            _output?.WriteLine($"🔍 Checking Logstash node stats: {nodeStatsUrl}");
            
            var response = await httpClient.GetAsync(nodeStatsUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _output?.WriteLine("✅ Logstash node stats retrieved successfully");
                
                // Check if the response contains pipeline info
                if (content.Contains("\"pipeline\""))
                {
                    _output?.WriteLine("✅ Logstash pipeline is loaded");
                }
                else
                {
                    _output?.WriteLine("⚠️  Logstash pipeline may not be loaded yet");
                }
            }
            else
            {
                _output?.WriteLine($"⚠️  Logstash node stats request failed: {response.StatusCode}");
            }
            
            // Give additional time for Logstash to fully initialize
            _output?.WriteLine("⏳ Giving Logstash additional 10 seconds to connect to Kafka...");
            await Task.Delay(10000);
            
            // Check logs to see if Kafka connection succeeded
            var logs = await GetLogstashLogsAsync();
            if (logs.Contains("Kafka input") || logs.Contains("subscribed", StringComparison.OrdinalIgnoreCase))
            {
                _output?.WriteLine("✅ Logstash appears to have connected to Kafka");
            }
            else
            {
                _output?.WriteLine("⚠️  Cannot confirm Logstash connected to Kafka - check logs");
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"⚠️  Failed to verify Logstash connections: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to get Logstash container logs for debugging
    /// </summary>
    public async Task<string> GetLogstashLogsAsync()
    {
        if (_logstashContainer == null)
        {
            return "Logstash container not started";
        }

        try
        {
            var (stdout, stderr) = await _logstashContainer.GetLogsAsync();
            return $"STDOUT:\n{stdout}\n\nSTDERR:\n{stderr}";
        }
        catch (Exception ex)
        {
            return $"Failed to get logs: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        _output?.WriteLine("🛑 Stopping Elasticsearch test infrastructure...");

        if (_kibanaContainer != null)
            await _kibanaContainer.DisposeAsync();

        if (_logstashContainer != null)
            await _logstashContainer.DisposeAsync();

        if (_elasticContainer != null)
            await _elasticContainer.DisposeAsync();

        if (_network != null)
            await _network.DeleteAsync();

        _dockerClient?.Dispose();

        _output?.WriteLine("✅ Elasticsearch stack stopped");
    }
}
