using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Azure.Cosmos;

/// <summary>
/// Testcontainers-based Azure Cosmos DB Emulator test infrastructure
/// Provides Cosmos DB Emulator for integration testing
/// Note: Cosmos DB Emulator is Windows-only. For Linux/Mac, use actual Azure Cosmos DB account for testing.
/// </summary>
public class CosmosTestContainer : IAsyncLifetime
{
    private readonly ITestOutputHelper? _output;
    private IContainer? _cosmosContainer;

    public string AccountEndpoint { get; private set; } = "https://localhost:8081";
    public string AccountKey { get; private set; } = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    public string DatabaseName { get; private set; } = "HospitalityOrdersTest";
    public bool UseEmulator { get; set; } = true;

    public CosmosTestContainer(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _output?.WriteLine("🚀 Starting Azure Cosmos DB Emulator...");

            if (OperatingSystem.IsWindows())
            {
                await StartCosmosEmulatorWindowsAsync();
            }
            else
            {
                await StartCosmosEmulatorLinuxAsync();
            }

            _output?.WriteLine("✅ Cosmos DB Emulator is ready!");
            _output?.WriteLine($"📊 Endpoint: {AccountEndpoint}");
            _output?.WriteLine($"📊 Database: {DatabaseName}");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"❌ Failed to start Cosmos DB Emulator: {ex.Message}");
            _output?.WriteLine("⚠️  Note: Cosmos DB Emulator may not be available in containers on this platform.");
            _output?.WriteLine("   For non-Windows platforms, configure actual Azure Cosmos DB for testing.");
            throw;
        }
    }

    private async Task StartCosmosEmulatorWindowsAsync()
    {
        // Try to use local Cosmos DB Emulator on Windows
        // Most Windows development environments have it installed
        _output?.WriteLine("✅ Using local Windows Cosmos DB Emulator");
        _output?.WriteLine("   Make sure Cosmos DB Emulator is running:");
        _output?.WriteLine("   - Start Menu → Azure Cosmos DB Emulator");
        _output?.WriteLine("   - Or run: 'C:\\Program Files\\Azure Cosmos DB Emulator\\CosmosDB.Emulator.exe'");

        // Verify emulator is accessible
        await VerifyEmulatorAccessAsync();
    }

    private async Task StartCosmosEmulatorLinuxAsync()
    {
        _output?.WriteLine("⏳ Starting Cosmos DB Emulator (Linux container)...");

        try
        {
            _cosmosContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
                .WithPortBinding(8081, true)
                .WithPortBinding(10251, true)
                .WithPortBinding(10252, true)
                .WithPortBinding(10253, true)
                .WithPortBinding(10254, true)
                .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "10")
                .WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "true")
                .WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(8081)
                        .ForPath("/")
                        .ForStatusCode(HttpStatusCode.OK)))
                .Build();

            await _cosmosContainer.StartAsync();
            
            // Get the dynamically assigned port
            var cosmosPort = _cosmosContainer.GetMappedPublicPort(8081);
            AccountEndpoint = $"https://localhost:{cosmosPort}";

            // Additional wait for emulator to fully initialize
            _output?.WriteLine("⏳ Waiting for Cosmos DB Emulator to initialize...");
            await Task.Delay(30000); // Emulator needs time to start

            await VerifyEmulatorAccessAsync();

            _output?.WriteLine($"✅ Cosmos DB Emulator container ready at {AccountEndpoint}");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"❌ Failed to start Cosmos DB Emulator container: {ex.Message}");
            _output?.WriteLine("   Falling back to local emulator configuration...");
            
            // Fallback to default local emulator
            AccountEndpoint = "https://localhost:8081";
            await VerifyEmulatorAccessAsync();
        }
    }

    private async Task VerifyEmulatorAccessAsync()
    {
        try
        {
            // Skip SSL validation for emulator
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var response = await httpClient.GetAsync(AccountEndpoint);
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _output?.WriteLine("✅ Cosmos DB Emulator is accessible");
            }
            else
            {
                _output?.WriteLine($"⚠️  Cosmos DB Emulator responded with: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"⚠️  Could not verify Cosmos DB Emulator access: {ex.Message}");
            _output?.WriteLine("   Tests may fail if emulator is not running.");
        }
    }

    public async Task DisposeAsync()
    {
        _output?.WriteLine("🛑 Stopping Cosmos DB Emulator test infrastructure...");

        if (_cosmosContainer != null)
        {
            await _cosmosContainer.DisposeAsync();
        }

        _output?.WriteLine("✅ Cosmos DB test infrastructure stopped");
    }
}
