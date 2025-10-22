using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Dapr;

/// <summary>
/// Integration tests for ASP.NET Core application with Dapr sidecar
/// Demonstrates full integration including pub/sub subscriptions and state management
/// </summary>
public class DaprWebApplicationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private DaprTestContainer _daprContainer = default!;
    private IHost _webHost = default!;
    private HttpClient _httpClient = default!;

    public DaprWebApplicationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Start Dapr infrastructure
            _daprContainer = new DaprTestContainer(_output, "webapp-test");
            await _daprContainer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw;
        }

        // Create test web application with Dapr
        _webHost = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        // Add Dapr client configured to use our test sidecar
                        services.AddSingleton<DaprClient>(_ => 
                            new DaprClientBuilder()
                                .UseHttpEndpoint(_daprContainer.DaprHttpEndpoint)
                                .UseGrpcEndpoint(_daprContainer.DaprGrpcEndpoint)
                                .Build());

                        services.AddControllers().AddDapr();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseCloudEvents();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapSubscribeHandler();
                            endpoints.MapControllers();

                            // Health check endpoint
                            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

                            // State management endpoints
                            endpoints.MapPost("/save-state", async (StateRequest request, DaprClient daprClient) =>
                            {
                                await daprClient.SaveStateAsync("statestore", request.Key, request.Value);
                                return Results.Ok(new { message = "State saved", key = request.Key });
                            });

                            endpoints.MapGet("/get-state/{key}", async (string key, DaprClient daprClient) =>
                            {
                                var value = await daprClient.GetStateAsync<string>("statestore", key);
                                return value != null 
                                    ? Results.Ok(new { key, value }) 
                                    : Results.NotFound();
                            });

                            // Pub/Sub endpoints
                            endpoints.MapPost("/publish", async (PublishRequest request, DaprClient daprClient) =>
                            {
                                await daprClient.PublishEventAsync("pubsub", request.Topic, request.Data);
                                return Results.Ok(new { message = "Event published", topic = request.Topic });
                            });

                            // Subscription endpoint (receives messages from Dapr)
                            endpoints.MapPost("/events", async (DaprData<MessageData> data) =>
                            {
                                Console.WriteLine($"📬 Received event: {data.Data.Content}");
                                return Results.Ok();
                            });
                        });
                    });
            })
            .StartAsync();

        _httpClient = _webHost.GetTestClient();
        _output.WriteLine("✅ Web application with Dapr started");
    }

    [Fact]
    public async Task WebApp_HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _httpClient.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"✅ Health check response: {content}");
    }

    [Fact]
    public async Task WebApp_SaveState_ShouldStoreInDapr()
    {
        // Arrange
        var request = new StateRequest
        {
            Key = "webapp-test-key",
            Value = "Hello from web app!"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/save-state", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SaveStateResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.Key, result.Key);
        
        _output.WriteLine($"✅ State saved via web app: {request.Key}");
    }

    [Fact]
    public async Task WebApp_GetState_ShouldRetrieveFromDapr()
    {
        // Arrange - First save some state
        var key = "webapp-retrieve-key";
        var value = "Test Value";
        
        await _httpClient.PostAsJsonAsync("/save-state", new StateRequest { Key = key, Value = value });

        // Act - Retrieve the state
        var response = await _httpClient.GetAsync($"/get-state/{key}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GetStateResponse>();
        Assert.NotNull(result);
        Assert.Equal(key, result.Key);
        Assert.Equal(value, result.Value);
        
        _output.WriteLine($"✅ State retrieved via web app: {result.Value}");
    }

    [Fact]
    public async Task WebApp_PublishEvent_ShouldSendToDapr()
    {
        // Arrange
        var publishRequest = new PublishRequest
        {
            Topic = "webapp-events",
            Data = new MessageData
            {
                Id = Guid.NewGuid(),
                Content = "Test message from web app",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/publish", publishRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<PublishResponse>();
        Assert.NotNull(result);
        Assert.Equal(publishRequest.Topic, result.Topic);
        
        _output.WriteLine($"✅ Event published via web app to topic: {publishRequest.Topic}");
    }

    [Fact]
    public async Task WebApp_StateManagement_EndToEnd()
    {
        // Arrange
        var testKey = $"e2e-test-{Guid.NewGuid():N}";
        var testValue = "End-to-End Test Value";

        // Act 1 - Save
        var saveResponse = await _httpClient.PostAsJsonAsync("/save-state", 
            new StateRequest { Key = testKey, Value = testValue });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        _output.WriteLine($"✅ Step 1: Saved state with key '{testKey}'");

        // Act 2 - Retrieve
        var getResponse = await _httpClient.GetAsync($"/get-state/{testKey}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetStateResponse>();
        Assert.NotNull(retrieved);
        Assert.Equal(testValue, retrieved.Value);
        _output.WriteLine($"✅ Step 2: Retrieved state: '{retrieved.Value}'");

        // Act 3 - Update
        var updatedValue = "Updated Value";
        var updateResponse = await _httpClient.PostAsJsonAsync("/save-state",
            new StateRequest { Key = testKey, Value = updatedValue });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        _output.WriteLine($"✅ Step 3: Updated state");

        // Act 4 - Verify update
        var verifyResponse = await _httpClient.GetAsync($"/get-state/{testKey}");
        var verified = await verifyResponse.Content.ReadFromJsonAsync<GetStateResponse>();
        Assert.NotNull(verified);
        Assert.Equal(updatedValue, verified.Value);
        _output.WriteLine($"✅ Step 4: Verified update: '{verified.Value}'");
    }

    [Fact]
    public async Task WebApp_GetNonExistentState_ShouldReturnNotFound()
    {
        // Act
        var response = await _httpClient.GetAsync("/get-state/non-existent-key");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("✅ Non-existent state correctly returned 404");
    }

    #region Helper Classes

    private record StateRequest
    {
        public string Key { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }

    private record PublishRequest
    {
        public string Topic { get; init; } = string.Empty;
        public MessageData Data { get; init; } = new();
    }

    private record MessageData
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
    }

    private record DaprData<T>
    {
        public T Data { get; init; } = default!;
    }

    private record SaveStateResponse
    {
        public string Message { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
    }

    private record GetStateResponse
    {
        public string Key { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }

    private record PublishResponse
    {
        public string Message { get; init; } = string.Empty;
        public string Topic { get; init; } = string.Empty;
    }

    #endregion

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        
        if (_webHost != null)
        {
            await _webHost.StopAsync();
            _webHost.Dispose();
        }

        if (_daprContainer != null)
        {
            await _daprContainer.DisposeAsync();
        }
    }
}
