using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Xunit.Abstractions;

public class IntegrationTestFactory<TProgram> : WebApplicationFactory<TProgram>, IDisposable
        where TProgram : class
{
    #region Private fields and consts

    private const string _outputTemplateDefault = "test [{Timestamp:HH:mm:ss.fff} {CorrelationId} {Level:u3}] {MachineName}{ClientIp}{ClientAgent}{ThreadId} {Method} {Username} {Title:lj}{NewLine}{Exception}";
    private string? _appsettingsApi = null;
    private string? _appsettingsTest = null;
    private IConfiguration? _configuration;
    private ITestOutputHelper? _output = null;
    private readonly IServiceScope? _serviceScope = null;
    private LogEventLevel[]? _loggerLevels = null;
    private string _testMethodDisplayName = "";
    private Action<WebHostBuilderContext, IServiceCollection>? _onConfigureTestServices;
    private readonly List<TaskCompletionSource<string>>? _taskSources = null;

    #endregion Private fields and consts

    #region Public props

    /// <summary> EnvironmentName </summary>
    public string EnvironmentName { get; set; } = "Test";

    /// <summary> Logger </summary>
    public ILogger Logger => _serviceScope?.ServiceProvider?.GetService<ILogger>() ?? throw new NotImplementedException($"{typeof(ILogger)} not implemented");

    /// <summary> Configuration </summary>
    public IConfiguration Configuration => _configuration ?? throw new NullReferenceException($"{typeof(IConfiguration)} not implemented");

    #endregion Public props

    #region Public configure

    /// <summary> ConfigureWebHost </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName)
            //.Configure(app => { app.UseMiddleware<ExceptionHandlerMiddleware>(); })
            .ConfigureAppConfiguration(config => GetConfiguration(config))
            .ConfigureServices(GetConfigureServices);
        base.ConfigureWebHost(builder);
    }

    // protected override IWebHostBuilder CreateWebHostBuilder() => WebHost.CreateDefaultBuilder(null).UseStartup<TStartup>();
    /// <summary> Configure services for test environment only, it occurs after api configure services </summary>
    public void ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> action)
    {
        _onConfigureTestServices = action;
    }

    /// <summary> Configure xunit output  </summary>
    public void ConfigureOutput(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary> Configure Logger levels, it will write levels params only </summary>
    public void ConfigureLoggerLevels(params LogEventLevel[] levels)
    {
        _loggerLevels = levels;
    }

    #endregion

    #region Public create

    /// <summary> MakeRequests special TaskPrefix guid of first PadLeft(8) characters </summary>
    public Guid CteateSpecialGuid(string prefix)
    {
        var guid = Guid.NewGuid().ToString("n");
        var result = new Guid(prefix?.PadLeft(8, '0') + guid[8..]);
        return result;
    }

    /// <summary> MakeRequests arranged data with anonymous data </summary>
    //public T CreateArrangeData<T>() where T : class
    //{
    //    return new Fixture().Create<T>();
    //}

    /// <summary> Creates an instance of HttpClient </summary>
    public HttpClient CreateClient([CallerMemberName] string callerMemberName = "")
    {
        _testMethodDisplayName = callerMemberName;
        return base.CreateClient();
    }

    /// <summary>Creates a <see cref="TaskCompletionSource{TResult}"/> with the specified state.</summary>
    public TaskCompletionSource<string> CreateCompletionSource()
    {
        // if (_taskSources == null)
        //     _taskSources = new List<TaskCompletionSource<string>>();
        var taskSource = new TaskCompletionSource<string>("");
        //_taskSources.Add(taskSource);
        return taskSource;
    }

    /// <summary> Wait to completion async operations </summary>
    public async Task<TResult> WaitCompletionSource<TResult>(TaskCompletionSource<TResult> taskCompletionSource,
        int timeoutFromSeconds = 10, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(timeoutFromSeconds);
        using (var timeoutCancellation = new CancellationTokenSource())
        using (var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, cancellationToken))
        {
            var operationTask = taskCompletionSource.Task;
            var timeoutTask = Task.Delay(timeout, combinedCancellation.Token);
            var completedTask = await Task.WhenAny(operationTask, timeoutTask);

            timeoutCancellation.Cancel();
            if (completedTask == operationTask)
            {
                return await operationTask;
            }
            else
            {
                taskCompletionSource.SetException(new TimeoutException("The operation has timed out."));
            }
#pragma warning disable CS8603 // Possible null reference return.
            return default;
#pragma warning restore CS8603 // Possible null reference return.
        }
    }

    /// <summary> Wait to completion async operations </summary>
    public async Task WaitAllCompletionSources(int timeFromSeconds = 20)
    {
        if (_taskSources == null) return;

        Logger.Information($"process wait to complete operation {timeFromSeconds} seconds ...");
        var tasks = _taskSources.Select(x => x.Task).ToArray();

        using (var cancellationTokenSource = new CancellationTokenSource())
        using (var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeFromSeconds)))
        using (var cancellationMonitorTask = Task.Delay(-1, cancellationTokenSource.Token))
        {
            Task completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask, cancellationMonitorTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("The operation has timed out.");
            }
            if (completedTask == cancellationMonitorTask)
            {
                throw new OperationCanceledException();
            }
            await completedTask;
        }
    }

    /// <summary> MakeRequests instance of factory </summary>
    public IntegrationTestFactory<TProgram> CreateFactory()
    {
        var factory = new IntegrationTestFactory<TProgram>();
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(config => { _configuration = GetConfiguration(config); });
            builder?.ConfigureServices((context, services) => GetConfigureServices(context, services));
        });
        return factory;
    }

    #endregion

    #region Public get methods

    /// <summary> Get service from service provider </summary>
    public T GetService<T>()
    {
        if (_serviceScope == null)
            throw new ArgumentNullException($"serviceScope is null");

        return _serviceScope.ServiceProvider.GetService<T>()
            ?? throw new ArgumentNullException($"service {typeof(T)} not registed");
    }

    /// <summary>GetObject from json file, from data directory </summary>
    public async Task<T?> GetObjectFromJsonAsync<T>(string filePath)
    {
        try
        {
            using var r = new StreamReader(filePath);
            string jsonString = await r.ReadToEndAsync();
            var result = JsonConvert.DeserializeObject<T>(jsonString);
            return result;
        }
        catch (Exception ex)
        {
            Logger?.Warning($"{nameof(GetObjectFromJson)} error: {ex.Message}.");
            return default;
        }
    }

    /// <summary>GetObject from json file with extensions </summary>
    public T? GetObjectFromJson<T>(string filePath)
    {
        try
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            Logger?.Warning($"{nameof(GetObjectFromJson)} error: {ex.Message}.");
            return default;
        }
    }

    /// <summary>GetObject from xml file </summary>
    public T? GetObjectFromXml<T>(string filePath)
    {
        try
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            using var requestFileStream = new FileStream(filePath, FileMode.Open);
            var xmlSerializer = new XmlSerializer(typeof(T));
            var result = (T?)xmlSerializer.Deserialize(requestFileStream);
            return result;
        }
        catch (Exception ex)
        {
            Logger?.Warning($"{nameof(GetObjectFromXml)} error: {ex.Message}.");
            return default;
        }
    }

    /// <summary> Get test displayName, by method name </summary>
    public string GetTestDisplayName()
    {
        if (_output != null)
        {
            var type = _output.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            ITest test = testMember?.GetValue(_output) as ITest ?? throw new NullReferenceException();
            return test.DisplayName;
        }

        return _testMethodDisplayName;
    }

    /// <summary> Define if current test case mach to params </summary>
    public bool TestCaseContains(params string[] testCaseParams)
    {
        var result = testCaseParams.All(t => GetTestDisplayName().ToLower().Contains(t.ToLower()));
        return result;
    }

    /// <summary> Executes an asynchronous action and aggregates its run time into the total. </summary>
    public TimeSpan Measure(Func<Task> action)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(0);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            action();
        }
        finally
        {
            elapsed = stopwatch.Elapsed;
        }

        return elapsed;
    }

    /// <summary> Executes an asynchronous action and aggregates its run time into the total. </summary>
    public async Task<TimeSpan> MeasureAsync(Func<Task> asyncAction)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(0);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await asyncAction();
        }
        finally
        {
            elapsed = stopwatch.Elapsed;
        }

        return elapsed;
    }

    #endregion Public get methods

    #region Privates methods

    private IConfiguration GetConfiguration(IConfigurationBuilder config)
    {
        _appsettingsApi = Path.Combine(Path.GetDirectoryName(typeof(TProgram).Assembly.Location) ?? "", $"appsettings.json");
        _appsettingsTest = Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{EnvironmentName}.json");

        config.AddJsonFile(_appsettingsApi, false);
        config.AddJsonFile(_appsettingsTest, true);
        _configuration = config.Build();
        return _configuration;
    }

    private void GetConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        if (_output != null)
        {
            services.AddSingleton(_output);

            var loggerConfiguration = new LoggerConfiguration()
               .ReadFrom.Configuration(_configuration ?? throw new NullReferenceException($"{typeof(IConfiguration)} not implemented"));

            var outputTemplate = _configuration?.GetSection("Serilog:WriteTo:0:Args:outputTemplate").Value ?? _outputTemplateDefault;

            Log.Logger = new LoggerConfiguration()
               //.WriteTo.TestOutput(_outputHelper, outputTemplate: outputTemplate)
               .Destructure.ByTransforming<HttpRequest>(r => new { r.Query, r.Method })
               .Enrich.FromLogContext()
               .CreateLogger();
        }

        //ILogger logger = _testBootstrap.CreateLogger(_outputHelper, _loggerLevels);
        services.Replace(ServiceDescriptor.Singleton(Log.Logger));

        // services customization for current test
        _onConfigureTestServices?.Invoke(context, services);

        // add exception filter
        services.AddControllers(options =>
        options.Filters.Add(new HttpResponseExceptionFilter()));

        // build service's scope
        //_serviceScope = services.BuildServiceProvider().CreateScope();

        Log.Logger.Information($"EnvironmentName: {context.HostingEnvironment.EnvironmentName}");
        Log.Logger.Information("Got configuration from api and test appsettings");
    }

    #endregion
}

