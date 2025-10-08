using Custom.Framework.Cache;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Extensions;
using Custom.Framework.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Custom.Framework.TestFactory.Core;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

public class TestHostBuilder<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    private Action<HostBuilderContext, IServiceCollection>? _onConfigureTestServices;
    
    public TestHostBuilder()
    {
        if (typeof(TStartup) == typeof(object))
        {
            HostBuilder = new HostBuilder();
            ConfigureLocalHost();
        }
    }

    /// <summary>
    /// Gets or sets the host builder used to configure and create the application's host.
    /// </summary>
    public HostBuilder HostBuilder { get; set; }

    public bool IsDebugMode { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (typeof(TStartup) != typeof(object))
        {
            base.ConfigureWebHost(builder);
        }

        //builder.ConfigureServices(services =>
        //{
        //    // Shared configuration for both scenarios
        //});
    }

    private void ConfigureLocalHost()
    {
        //HostBuilder.ConfigureServices(services =>
        //{
        //    // Shared or custom configuration
        //});

        HostBuilder.UseEnvironment("Test");

        HostBuilder.ConfigureAppConfiguration(ConfigureTestAppConfiguration());

        HostBuilder.ConfigureServices(ConfigureTestServices());

        //HostBuilder.ConfigureWebHost(ConfigureTestWebHost());

    }

    //protected override void ConfigureWebHost(IWebHostBuilder builder)
    //{
    //    builder.Configure(app =>
    //    {
    //        app.UseStaticDataLoadByRequest();
    //    });
    //}

    // temporary

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration(ConfigureTestAppConfiguration());

        builder.ConfigureServices(ConfigureTestServices());

        builder.ConfigureWebHost(ConfigureTestWebHost());

        var host = base.CreateHost(builder);

        return host;
    }

    private static Action<IWebHostBuilder> ConfigureTestWebHost()
    {
        return webHost =>
        {
            //webHost.UseTestServer();
            webHost.UseStartup<TestStartup>();
        };
    }

    private static Action<HostBuilderContext, IConfigurationBuilder> ConfigureTestAppConfiguration()
    {
        return (builderContext, builderConfig) =>
        {
            var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
            var env = builderContext.HostingEnvironment;
            //builderContext.Properties.Add("IsDebugMode", _isDebugMode);

            var environmentName = env.EnvironmentName;
            var contentRootPath = env.ContentRootPath;
            //var fileProvider = env.ContentRootFileProvider;
            //var changeToken = fileProvider.Watch(fileName);
            builderConfig
                .AddJsonFile(Path.Combine(directory, $"appsettings.json"), optional: false)
                .AddJsonFile(Path.Combine(directory, $"appsettings.{environmentName}.json"), optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        };
    }

    private Action<HostBuilderContext, IServiceCollection> ConfigureTestServices()
    {
        return (context, services) =>
        {
            _onConfigureTestServices?.Invoke(context, services);
            ConfigureMoqWebApplicationOptions(services, context.Configuration);
            ConfigureMoqHttpContext(services);
            services.ConfigureEnvironments(context.Configuration);
            //services.ConfigureSettings(context.Configuration);
            services.ConfigureCache(context.Configuration);
            //services.ConfigureSwagger(context.Configuration);
            //services.ConfigureLogging(context.Configuration);
            //services.ConfigureHttpClients(context.Configuration);
            //services.ConfigureHealthChecks(context.Configuration);
        };
    }

    /// <summary> Configure services for test environment only, it occurs after api configure services </summary>
    public void ConfigureServices(Action<HostBuilderContext, IServiceCollection> action)
    {
        _onConfigureTestServices = action;
    }

    private void ConfigureMoqWebApplicationOptions(IServiceCollection services, IConfiguration config)
    {
        var optionsMock = new Mock<IOptions<WebApplicationOptions>>();
        var webAppOptions = new WebApplicationOptions
        {
            EnvironmentName = "Test",
            Args = [$"IsDebugMode={IsDebugMode}"],
        };
        optionsMock.Setup(o => o.Value).Returns(webAppOptions);
        services.AddSingleton((sp) => optionsMock.Object);
    }

    private void ConfigureMoqHttpContext(IServiceCollection services)
    {
        var httpContext = new DefaultHttpContext();
        var HttpRequestFeature = new HttpRequestFeature
        {
            Protocol = "HTTP/1.1",
            Scheme = "http",
            Method = "GET",
            Path = "/Search",
            PathBase = "",
            Headers = new HeaderDictionary(),
            QueryString = "?data=1",
        };

        var testGuid = Guid.NewGuid().UpdateSection("00000000").ToString();
        httpContext.Features.Set<IHttpRequestFeature>(HttpRequestFeature);
        httpContext.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        var items = (IDictionary<object, object?>)new Dictionary<object, object>();
        items.Add("IsDebugMode", IsDebugMode);
        httpContext.Items = items;

        // httpRequest
        var httpRequestMock = new Mock<HttpRequest>();
        httpRequestMock.Setup(r => r.Method).Returns("GET");
        var headersMock = new HeaderDictionary { { RequestHeaderKeys.CorrelationId, testGuid } };
        httpRequestMock.Setup(r => r.Headers).Returns(headersMock);

        // isDebugMode
        var queryCollection = new QueryCollection(
            new Dictionary<string, StringValues> { { "isDebugMode", IsDebugMode.ToString() } });
        httpRequestMock.Setup(r => r.Query).Returns(queryCollection);

        // httpContextAccessor
        services.AddScoped<IHttpContextAccessor>((sp) => {
            var serviceProvider = services.BuildServiceProvider();
            var _httpContextAccessorMoq = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            _httpContextAccessorMoq.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);
            _httpContextAccessorMoq.Setup(x => x.HttpContext!.RequestServices).Returns(() => serviceProvider);
            _httpContextAccessorMoq.Setup(m => m.HttpContext!.Items).Returns(items!);
            _httpContextAccessorMoq.Setup(a => a.HttpContext!.Request).Returns(httpRequestMock.Object);
            return _httpContextAccessorMoq.Object;
        });
    }

    public TestServer Build()
    {
        //var host = CreateTestHost(new string[] { "environment=Test" });
        //var testServer = new TestServer(host);
        //return testServer;
        return Server;
    }
}

public class TestHostBuilder
{
    private bool _isDebugMode;
    private readonly HttpClient _client;
    private Action<HostBuilderContext, IServiceCollection>? _onConfigureTestServices;

    public TestHostBuilder(bool isDebugMode = false)
    {
        _isDebugMode = isDebugMode;

        var builder = new HostBuilder();

        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((builderContext, builderConfig) =>
        {
            var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
            var env = builderContext.HostingEnvironment;
            //builderContext.Properties.Add("IsDebugMode", _isDebugMode);

            var environmentName = env.EnvironmentName;
            var contentRootPath = env.ContentRootPath;
            //var fileProvider = env.ContentRootFileProvider;
            //var changeToken = fileProvider.Watch(fileName);
            builderConfig
                .AddJsonFile(Path.Combine(directory, $"appsettings.json"), optional: false)
                .AddJsonFile(Path.Combine(directory, $"appsettings.{environmentName}.json"), optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        });

        builder.ConfigureServices((context, services) =>
        {
            _onConfigureTestServices?.Invoke(context, services);
            ConfigureMoqWebApplicationOptions(services, context.Configuration);
            ConfigureMoqHttpContext(services);
            services.ConfigureEnvironments(context.Configuration);
            services.ConfigureSettings(context.Configuration);
            services.ConfigureCache(context.Configuration);
            //services.ConfigureSwagger(context.Configuration);
            //services.ConfigureLogging(context.Configuration);
            //services.ConfigureHttpClients(context.Configuration);
            //services.ConfigureHealthChecks(context.Configuration);
        });

        builder.ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureServices(services =>
            {
                services.AddControllers();
            });
            webBuilder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/test", async context =>
                    {
                        await context.Response.WriteAsync("Hello from In-Memory API!");
                    });
                });
            });
        });

        builder.ConfigureWebHost(webHost =>
        {
            //webHost.UseTestServer();
            webHost.UseStartup<TestStartup>();
        });

        IHost host = builder.Start();
        _client = host.GetTestClient();
    }

    private void ConfigureMoqWebApplicationOptions(IServiceCollection services, IConfiguration config)
    {
        var optionsMock = new Mock<IOptions<WebApplicationOptions>>();
        var webAppOptions = new WebApplicationOptions
        {
            EnvironmentName = "Test",
            Args = [$"IsDebugMode={_isDebugMode}"],
        };
        optionsMock.Setup(o => o.Value).Returns(webAppOptions);
        services.AddSingleton((sp) => optionsMock.Object);
    }

    private void ConfigureMoqHttpContext(IServiceCollection services)
    {
        var httpContext = new DefaultHttpContext();
        var HttpRequestFeature = new HttpRequestFeature
        {
            Protocol = "HTTP/1.1",
            Scheme = "http",
            Method = "GET",
            Path = "/Search",
            PathBase = "",
            Headers = new HeaderDictionary(),
            QueryString = "?data=1",
        };

        var testGuid = Guid.NewGuid().UpdateSection("00000000").ToString();
        httpContext.Features.Set<IHttpRequestFeature>(HttpRequestFeature);
        httpContext.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        var items = (IDictionary<object, object?>)new Dictionary<object, object>();
        items.Add("IsDebugMode", _isDebugMode);
        httpContext.Items = items;

        // httpRequest
        var httpRequestMock = new Mock<HttpRequest>();
        httpRequestMock.Setup(r => r.Method).Returns("GET");
        var headersMock = new HeaderDictionary { { RequestHeaderKeys.CorrelationId, testGuid } };
        httpRequestMock.Setup(r => r.Headers).Returns(headersMock);

        // isDebugMode
        var queryCollection = new QueryCollection(
            new Dictionary<string, StringValues> { { "isDebugMode", _isDebugMode.ToString() } });
        httpRequestMock.Setup(r => r.Query).Returns(queryCollection);

        // httpContextAccessor
        var serviceProvider = services.BuildServiceProvider();
        var _httpContextAccessorMoq = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        _httpContextAccessorMoq.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);
        _httpContextAccessorMoq.Setup(x => x.HttpContext!.RequestServices).Returns(() => serviceProvider);
        _httpContextAccessorMoq.Setup(m => m.HttpContext!.Items).Returns(items!);
        _httpContextAccessorMoq.Setup(a => a.HttpContext!.Request).Returns(httpRequestMock.Object);
        services.AddScoped((sp) => _httpContextAccessorMoq.Object);
    }

    //[Fact]
    //public async void GetTestEndpoint_ReturnsExpectedResponse()
    //{
    //    // Act
    //    var response = await _client.GetAsync("/api/test");
    //    response.EnsureSuccessStatusCode();
    //    var content = await response.Content.ReadAsStringAsync();

    //    // Assert
    //    Assert.Equal("Hello from In-Memory API!", content);
    //}
}

