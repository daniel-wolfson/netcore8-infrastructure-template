using Custom.Framework.Configuration;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Serilog;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace Custom.Framework.TestFactory.Core;

/// <summary>
/// TestHostBase is typically used in integration tests 
/// to set up a test server with a specific configuration and services. 
/// It allows for:
/// - Customizing service configurations.
/// - Accessing application settings and services.
/// - Logging test activities.
/// </summary>
public class TestHostBase : IDisposable
{
    /// <summary> Serilog logger </summary>
    protected ILogger Logger;

    /// <summary> Current TestName </summary>
    protected string TestName;

    /// <summary> AppSettings from appSetting  </summary>
    protected ApiSettings AppSettings;

    /// <summary> ServiceProvider </summary>
    protected IServiceProvider ServiceProvider;

    /// <summary> it is IConfiguration implementation </summary>
    protected IConfiguration Configuration;

    private readonly ITestOutputHelper? _output;

    /// <summary> ctor </summary>
    public TestHostBase() { }

    /// <summary> ctor, ITestOutputHelper is injected by xUnit </summary>
    public TestHostBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// CreateTestHost - Create a test host for the given <typeparamref name="TStart"/> type.
    /// </summary>
    public TestServer CreateTestHost<TStart>(
        Action<HostBuilderContext, IServiceCollection>? onConfigureServices = null,
        Action<IServiceProvider>? onAppBuilding = null,
        [CallerMemberName] string testName = "") where TStart : class
    {
        var testHostBuilder = new TestHostBuilder<TStart>();

        testHostBuilder.ConfigureServices((context, services) =>
        {
            var logger = new TestLoggerWrapper(_output);
            services.Replace(new ServiceDescriptor(typeof(ILogger), logger, ServiceLifetime.Singleton));
            services.AddSingleton<ILogger>(provider => logger);
            onConfigureServices?.Invoke(context, services);
        });

        var testServer = typeof(TStart) != typeof(object)
            ? testHostBuilder.Build()
            : new TestServer(testHostBuilder.HostBuilder.Start().Services);

        ServiceProvider = testServer.Services;
        TestName = testName;
        AppSettings = ServiceProvider.GetRequiredService<IOptions<ApiSettings>>().Value;
        Logger = ServiceProvider.GetRequiredService<ILogger>();
        Configuration = ServiceProvider.GetService<IConfiguration>()!;
        
        onAppBuilding?.Invoke(ServiceProvider);

        Console.WriteLine("");
        Logger?.Information("{TEST} started.", testName);

        return testServer;
    }

    /// <summary>
    /// CreateTestHost - Create a test host for the given <typeparamref name="TStart"/> type.
    /// </summary>
    public TestServer CreateTestHost<TStart>(
        Action<IServiceProvider>? onAppBuilding,
        [CallerMemberName] string testName = "") where TStart : class
    {
        return CreateTestHost<TStart>(null, onAppBuilding, testName);
    }

    /// <summary>
    /// Get service of type <typeparamref name="T"/> from the <see cref="IServiceProvider"/>.
    /// </summary>
    public T? GetService<T>(string? keyedServiceName = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(keyedServiceName))
                return ServiceProvider.GetKeyedService<T>(keyedServiceName);

            return ServiceProvider.GetService<T>();
        }
        catch (Exception ex)
        {
            throw new ApiException(ex);
        }

    }

    /// <summary>
    /// CallPrivateMethod - Call a private method on an object.
    /// </summary>
    public static T? CallPrivateMethod<T>(object owner, string methodName, params object[] args)
    {
        var mi = owner.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (mi != null)
            return (T?)mi.Invoke(owner, args);

        return default;
    }

    /// <summary>
    /// IDispose implementation
    /// </summary>
    public void Dispose()
    {
        Logger?.Information("{TEST} disposed.", TestName);
        GC.SuppressFinalize(this);
    }
}
