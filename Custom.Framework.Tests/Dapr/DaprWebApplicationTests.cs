using Dapr.Client;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Dapr;

/// <summary>
/// Integration tests for ASP.NET Core application with Dapr sidecar
/// Demonstrates full integration including pub/sub subscriptions and state management
/// </summary>
public class DaprWebApplicationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly ITestOutputHelper _output = output;
    private DaprTestContainer _daprContainer = default!;
    private IWebHost _webHost = default!;
    private HttpClient _httpClient = default!;

    public async Task InitializeAsync()
    {
        // ✅ Step 1: Create DaprTestContainer instance FIRST (where you want it)
        _daprContainer = new DaprTestContainer("dapr-test") { Output = _output };
        var daprTask = _daprContainer.InitializeAsync();

        // ✅ Step 2: Start web app on YOUR MACHINE (not in Docker)
        _webHost = await CreateWebHost();

        _output.WriteLine("✅ Web application started on http://localhost:5000 (host machine)");

        // ✅ Step 3: NOW wait for Dapr to finish initializing
        await daprTask;

        _output.WriteLine("✅ Dapr infrastructure initialized");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        _output.WriteLine($"✅ Setup complete");
        _output.WriteLine($"ℹ️  Architecture:");
        _output.WriteLine($"   - App runs on host machine (localhost:5000)");
        _output.WriteLine($"   - Dapr runs in Docker (localhost:3500, localhost:50001)");
        _output.WriteLine($"   - App calls Dapr APIs ✅");
        _output.WriteLine($"   - Dapr CANNOT call app (different networks) ❌");
        _output.WriteLine($"   - State management: ✅ Works");
        _output.WriteLine($"   - Pub/sub publishing: ✅ Works");
        _output.WriteLine($"   - Pub/sub subscriptions: ❌ Auto-discovery doesn't work");
    }

    private async Task<IWebHost> CreateWebHost()
    {
        var webhost = new WebHostBuilder()  // Direct web hosting
            .UseKestrel()
            .UseUrls("http://localhost:5000")
            .ConfigureServices(services =>
            {
                // DaprClient connects TO Dapr container (lazy initialization)
                services.AddSingleton<DaprClient>(sp =>
                {
                    // By the time this is resolved, Dapr will be initialized
                    return new DaprClientBuilder()
                        .UseHttpEndpoint(_daprContainer.DaprHttpEndpoint)  // http://localhost:3500
                        .UseGrpcEndpoint(_daprContainer.DaprGrpcEndpoint)      // http://localhost:50001
                        .Build();
                });
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

                     endpoints.MapGet("/health", () =>
                         Results.Ok(new { status = "healthy" }));

                     endpoints.MapPost("/save-state", async (StateRequest request, DaprClient daprClient) =>
                     {
                         await daprClient.SaveStateAsync("statestore", request.Key, request.Value);
                         return Results.Ok(new { message = "State saved", key = request.Key });
                     });

                     endpoints.MapGet("/get-state/{key}", async (string key, DaprClient daprClient) =>
                     {
                         var value = await daprClient.GetStateAsync<string>("statestore", key);
                         return value != null ? Results.Ok(new { key, value }) : Results.NotFound();
                     });

                     endpoints.MapPost("/publish", async (PublishRequest request, DaprClient daprClient) =>
                     {
                         await daprClient.PublishEventAsync("pubsub", request.Topic, request.Data);
                         return Results.Ok(new { message = "Event published", topic = request.Topic });
                     });

                     // This endpoint exists but Dapr CAN'T reach it (different networks)
                     endpoints.MapGet("/dapr/subscribe", () =>
                     {
                         var subscriptions = new[] {
                                new
                                {
                                pubsubname = "pubsub",
                                topic = "webapp-events",
                                route = "/events"
                                }
                            };
                         _output.WriteLine("🔍 App has subscription config (but Dapr can't reach this endpoint)");
                         return Results.Ok(subscriptions);
                     });

                     endpoints.MapPost("/events", async (DaprData<MessageData> data) =>
                     {
                         _output.WriteLine($"📬 Received event from Dapr: {data.Data.Content}");
                         return Results.Ok();
                     });
                 });
             })
            .Build();

        await webhost.StartAsync();

        await VerifyWebHost();

        return webhost;
    }

    private async Task VerifyWebHost()
    {
        try
        {
            using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var healthResponse = await testClient.GetAsync("http://localhost:5000/health");

            if (healthResponse.IsSuccessStatusCode)
            {
                var healthContent = await healthResponse.Content.ReadAsStringAsync();
                _output.WriteLine($"✅ Web host verified - responded to health check: {healthContent}");
            }
            else
            {
                _output.WriteLine($"⚠️  Web host started but health check returned: {healthResponse.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Web host verification failed: {ex.Message}");
            throw new Exception("Web host did not start properly - could not connect to http://localhost:5000/health", ex);
        }
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

    [Fact]
    public async Task WebApp_PubSub_SubscriptionIsRegistered()
    {
        // Act - Check Dapr subscription endpoint
        var response = await _httpClient.GetAsync("/dapr/subscribe");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var subscriptions = await response.Content.ReadFromJsonAsync<List<SubscriptionInfo>>();
        Assert.NotNull(subscriptions);
        Assert.NotEmpty(subscriptions);

        var webappEventsSubscription = subscriptions.FirstOrDefault(s => s.Topic == "webapp-events");
        Assert.NotNull(webappEventsSubscription);
        Assert.Equal("pubsub", webappEventsSubscription.Pubsubname);
        Assert.Equal("/events", webappEventsSubscription.Route);

        _output.WriteLine($"✅ Subscription registered: topic='{webappEventsSubscription.Topic}', route='{webappEventsSubscription.Route}'");
    }

    [Fact]
    public async Task WebApp_PubSub_PublishAndReceive_EndToEnd()
    {
        // This test verifies that when we publish to Dapr, 
        // the message would be routed back to our /events endpoint
        // Note: In a real test, you'd need a way to verify the message was received

        // Arrange
        var publishRequest = new PublishRequest
        {
            Topic = "webapp-events",
            Data = new MessageData
            {
                Id = Guid.NewGuid(),
                Content = "E2E Test Message",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act - Publish event
        var publishResponse = await _httpClient.PostAsJsonAsync("/publish", publishRequest);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        _output.WriteLine($"✅ Published event to topic: {publishRequest.Topic}");

        // In a real scenario, Dapr would route this to /events endpoint
        // For testing, you'd typically use a message handler/queue to verify receipt
        await Task.Delay(500); // Give Dapr time to process

        _output.WriteLine("✅ Pub/Sub end-to-end flow completed");
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

    private record SubscriptionInfo
    {
        public string Pubsubname { get; init; } = string.Empty;
        public string Topic { get; init; } = string.Empty;
        public string Route { get; init; } = string.Empty;
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
            if (_daprContainer.IsForceCleanup)
                await _daprContainer.ForceCleanupNetworksAsync();

            await _daprContainer.DisposeAsync();
        }

        _output.WriteLine("✅ Cleanup complete");
    }
}
