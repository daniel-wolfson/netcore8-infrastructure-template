using FluentAssertions;
using Custom.Framework.Cache;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.TestFactory.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Custom.Framework.Tests;

/// <summary>
/// RedisCacheTests - test class for RedisCache using xunit
/// Warning: call references of CreateTestHost<object> need to be replaced with CreateTestHost<ApiControllerClass>
/// </summary>
/// <param name="output"></param>
public class RedisCacheTests(ITestOutputHelper output) : TestHostBase(output)
{
    [Fact]
    public async Task Redis_connection_positiveTest()
    {
        try
        {
            // Arrange
            var testHost = CreateTestHost<object>();
            var redisCache = testHost.Services.GetService<IRedisCache>();
            var logger = testHost.Services.GetService<ILogger>();

            //Act
            var isConnected = redisCache!.IsConnected();
            var expectedData = $"testData_{DateTime.Now:dd-MM-yyyy_hh-mm-ss}";
            await redisCache.SetAsync("testKey", expectedData, 5000);
            var actualData = await redisCache.GetAsync<string>("testKey");

            // Assert
            isConnected.Should()
               .BeTrue($"{nameof(Redis_connection_positiveTest)} failed: Redis is not connected");
            (actualData == expectedData).Should()
                .BeTrue($"{nameof(Redis_connection_positiveTest)} failed: expectedData: {expectedData} not equal to actualData: {actualData}");
        }
        catch (Exception)
        {
            throw;
        }
    }

    [Fact]
    public void Set_get_positiveTest()
    {
        // Arrange
        var testHost = CreateTestHost<object>();
        var redisCache = testHost.Services.GetRequiredService<IRedisCache>();
        var logger = testHost.Services.GetService<ILogger>();

        // Act
        var expectedData = $"testData_{DateTime.Now:dd-MM-yyyy_hh-mm-ss}";
        redisCache.Set("testKey", expectedData, 5000);
        var actualData = redisCache.Get<string>("testKey");

        // Assert
        var isValid = expectedData == actualData;
        isValid.Should().BeTrue($"{nameof(Set_get_positiveTest)} Get is {isValid}");
        logger?.Information("{TEST} Assert: expectedData equals actualData", nameof(Set_get_positiveTest));
    }

    [Fact]
    public async Task Set_get_async_positiveTest()
    {
        // Arrange
        var testHost = CreateTestHost<object>();
        var redisCache = testHost.Services.GetRequiredService<IRedisCache>();
        var logger = testHost.Services.GetService<ILogger>();

        // Act
        var isConnected = redisCache.IsConnected();
        var expectedData = $"testData_{DateTime.Now:dd-MM-yyyy_hh-mm-ss}";
        await redisCache.SetAsync("testKey", expectedData, 5000);
        var actualData = await redisCache.GetAsync<string>("testKey");

        // Assert
        var isValid = expectedData == actualData;
        logger?.Information("{TEST} end...{RESULT} is ", nameof(Set_get_async_positiveTest), isValid ? "OK" : "FAIL");
        isValid.Should().BeTrue($"{nameof(Set_get_async_positiveTest)} Get is {isValid}");
        logger?.Information("{TEST} Assert: expectedData equals actualData", nameof(Set_get_async_positiveTest));
    }

    [Fact]
    public async Task Set_remove_async_positiveTest()
    {
        // Arrange
        var testHost = CreateTestHost<object>();
        var redisCache = testHost.Services.GetRequiredService<IRedisCache>();
        var logger = testHost.Services.GetService<ILogger>();
        var keyName = "testKey";

        // Act
        await redisCache.SetAsync(keyName, $"testData_{DateTime.Now:dd-MM-yyyy_hh-mm-ss}", 5000);
        await redisCache.RemoveAsync(keyName);
        var actualDataExist = await redisCache.ExistsAsync(keyName);

        // Assert
        actualDataExist.Should().BeFalse($"{nameof(Set_get_positiveTest)} ExistsAsync is {actualDataExist}");
        logger?.Information("{TEST} Assert: key does not exist", nameof(Set_remove_async_positiveTest));
    }

    [Fact]
    public void FlushDb_positiveTest()
    {
        // arange
        var host = CreateTestHost<object>();
        var redisCache = host.Services.GetRequiredService<IRedisCache>();
        var logger = host.Services.GetService<ILogger>();

        //act
        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            var prevInitTimeStamp = redisCache.ReconnectTimeStamp;
            ((IRedisCacheFlush)redisCache).FlushDb();
            var isConnected = redisCache.IsConnected();
            var lastInitTimeStamp = redisCache.ReconnectTimeStamp;

            // assert
            var isValid = lastInitTimeStamp != prevInitTimeStamp;
            if (!isValid)
            {
                logger?.Error($"Redis ClearCache not working");
            }
            results.Add(isValid);
        }

        // assert
        logger?.Information("{TEST} end. {RESULT}", results.All(x => x) ? "OK" : "FAIL", nameof(FlushDb_positiveTest));
        results.ForEach(b => b.Should().BeTrue());
        logger?.Information("{TEST} Assert: all results are true", nameof(FlushDb_positiveTest));
    }

    [Fact]
    public async Task HashEntry_positiveTest()
    {
        //Arrange
        var host = CreateTestHost<object>();
        var redisCache = host.Services.GetRequiredService<IRedisCache>();
        var logger = host.Services.GetService<ILogger>();

        //Act
        var hashKey = "hashKey";
        var expectedValue = 2023;

        HashEntry[] redisBookHash = {
            new HashEntry("title", "Redis"),
            new HashEntry("year", expectedValue),
            new HashEntry("author", "Yosi Dock")
          };

        redisCache.HashSet(hashKey, redisBookHash, TTL.OneHour);

        var isValid = redisCache.HashExists(hashKey, "year");
        isValid.Should().BeTrue($"{Utils.LogTitle()} HashExists {isValid}");
        logger?.Information("{TEST} Assert: HashExists is true", nameof(HashEntry_positiveTest));

        double year = redisCache.HashGet<int>(hashKey, "year");
        isValid = year == expectedValue;
        isValid.Should().BeTrue($"{Utils.LogTitle()} HashGet {isValid}");
        logger?.Information("{TEST} Assert: HashGet is true", nameof(HashEntry_positiveTest));

        var allHash = await redisCache.HashGetAllAsync(hashKey);
        isValid = isValid && allHash?.Length > 0;
        isValid.Should().BeTrue($"{Utils.LogTitle()} HashGetAllAsync {isValid}");
        logger?.Information("{TEST} Assert: HashGetAllAsync is true", nameof(HashEntry_positiveTest));

        if (redisCache.HashExists(hashKey, "year"))
        {
            year = redisCache.HashIncrement(hashKey, "year", 1); //year now becomes 2017
            isValid = isValid && year == expectedValue + 1;
            isValid.Should().BeTrue($"{Utils.LogTitle()} HashGetAllAsync {isValid}");
            logger?.Information("{TEST} Assert: HashIncrement is true", nameof(HashEntry_positiveTest));

            var year2 = redisCache.HashDecrement(hashKey, "year", 1.5); //year now becomes 2015.5
            isValid = isValid && year2 == expectedValue - 0.5;
        }

        // Assert
        isValid.Should().BeTrue($"{Utils.LogTitle()} HashDecrement {isValid}");
        logger?.Information("{TEST} Assert: HashDecrement is true", nameof(HashEntry_positiveTest));
    }

    [Fact]
    public async Task Reconnect_positiveTest()
    {
        // Arrange
        //opera-ows-train.isrotel.co.il
        int MAX_ITERATIONS = 1;
        int MAX_PARALLEL_REQUESTS = 1;
        int DELAY = 1000 * 3;

        //Act
        var host = CreateTestHost<object>();
        var redisCache = host.Services.GetRequiredService<IRedisCache>();
        var logger = host.Services.GetService<ILogger>()!;

        logger.Information("{TEST} params: MAX_ITERATIONS - {MAX_ITERATIONS}, MAX_PARALLEL_REQUESTS - {MAX_PARALLEL_REQUESTS}",
            nameof(Reconnect_positiveTest), MAX_ITERATIONS, MAX_PARALLEL_REQUESTS);

        ((IRedisCacheFlush)redisCache).FlushDb();
        List<Task<bool>> setTasks = [];
        List<string> setTasksValues = [];
        List<Task<string?>> getTasks = [];
        List<Task<bool>> reconnectTasks = [];
        List<bool> results = [];

        try
        {
            for (var step = 0; step < MAX_ITERATIONS; step++)
            {
                setTasksValues = [];
                logger?.Information($"Started iteration step: {step++} of {MAX_PARALLEL_REQUESTS} requests");

                for (int i = 0; i < MAX_PARALLEL_REQUESTS; i++)
                {
                    if (i <= MAX_PARALLEL_REQUESTS - 1)
                    {
                        reconnectTasks.Add(redisCache.ReconnectAsync(TimeSpan.FromSeconds(DELAY)));
                        //await Task.Delay(DELAY);
                    }

                    setTasksValues.Add(i.ToString());
                    setTasks.Add(redisCache.SetAsync($"tehKey{i}", i.ToString(), 3600));
                }

                results.AddRange(await Task.WhenAll(reconnectTasks));
                results.AddRange(await Task.WhenAll(setTasks));

                for (int i = 0; i < MAX_PARALLEL_REQUESTS; i++)
                {
                    getTasks.Add(redisCache.GetAsync<string>($"tehKey{i}"));
                }

                // run all tasks
                List<string> getTasksResults = [];
                var tasksRsults = await Task.WhenAll(getTasks);
                if (tasksRsults != null && tasksRsults.Length != 0)
                    getTasksResults.AddRange(tasksRsults!);

                results.AddRange(getTasksResults.Select(x => x != null && setTasksValues.Contains(x)));

                results.ForEach(x => x.Should().BeTrue());
                logger?.Information("{TEST} Assert: all results are true", nameof(Reconnect_positiveTest));
                logger?.Information($"Complete iteration step: {step++} of {MAX_PARALLEL_REQUESTS} requests");

                // Some delay before new iteration
                await Task.Delay(DELAY);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        finally
        {
            results.Should().NotBeEmpty();
            results.Should().HaveCountGreaterThan(0);
            results.ForEach(result => result.Should().BeTrue("result should be true"));
            logger?.Information("{TEST} Assert: all results are true", nameof(Reconnect_positiveTest));
        }
    }

    [Fact]
    public async Task RetryPolicy_PositiveTest()
    {
        // Arange
        var retryPolicy = new ApiRetryPolicyBuilder()
            .WithRetryCount(3)
            .WithRetryInterval(TimeSpan.FromSeconds(2))
            .BuildAsyncPolicy<string>(); // Specify the return type of the operation you want to retry

        // Act
        try
        {
            await retryPolicy.ExecuteAsync(() =>
            {
                // Simulate a potentially failing operation
                if (DateTime.Now.Second % 2 == 0)
                {
                    Console.WriteLine("Operation failed.");
                    throw new Exception("Simulated error.");
                }

                // Assert
                Console.WriteLine("Operation succeeded.");
                return Task.FromResult("Result");
            });
        }
        catch (Exception ex)
        {
            // Assert
            Console.WriteLine($"All retries failed. Last exception: {ex.Message}");
        }
    }

    private async Task WaitForRedisConnectionWithEventAsync(IConnectionMultiplexer connection)
    {
        var tcs = new TaskCompletionSource<bool>();

        connection.ConnectionRestored += (_, _) => tcs.TrySetResult(true);
        connection.ConnectionFailed += (_, args) =>
        {
            Console.WriteLine($"Redis connection failed: {args?.Exception?.Message ?? ""}");
        };

        if (!connection.IsConnected)
        {
            Console.WriteLine("Waiting for Redis connection...");
            await tcs.Task; // Wait for the connection to be restored
        }

        Console.WriteLine("Redis connected!");
    }
}
