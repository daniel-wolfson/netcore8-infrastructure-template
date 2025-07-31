using Autofac;
using Autofac.Extensions.DependencyInjection;
using Custom.Framework.Configuration;
using Custom.Framework.Extensions;
using Custom.Framework.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Custom.Framework.Cache;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Custom.Framework.TestFactory.Core
{
    public partial class FunctionalTestFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        public Action<IServiceCollection, IConfiguration> OnConfigureTestServices = (sp, config) => { };
        private IServiceScope? _serviceScope;

        public IConfiguration? Configuration { get; set; }

        public ApiSettings AppSettings => GetService<IOptions<ApiSettings>>()?.Value ?? throw new ApiException("_appSettings not defined");
        public ApiSettings ApiSettings => GetService<IOptions<ApiSettings>>()?.Value ?? throw new ApiException("_appSettings not defined");

        /// <summary> EnvironmentName </summary>
        public string EnvironmentName { get; set; } = "Test";

        public TestServer CreateServer()
        {

            return Server;
        }

        public static TestServer CreateServer<TStartupType>() where TStartupType : class
        {
            var factory = new FunctionalTestFactory<TStartupType>();
            return factory.CreateServer();
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);

            builder.ConfigureAppConfiguration(config =>
            {
                var _appsettingsApi = Path.Combine(Path.GetDirectoryName(typeof(TStartup).Assembly.Location) ?? "", $"appsettings.json");
                var _appsettingsTest = Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{EnvironmentName}.json");

                config.AddJsonFile(_appsettingsApi, false);
                config.AddJsonFile(_appsettingsTest, true);
                Configuration = config.Build();
            });

            builder.ConfigureServices(services =>
            {
                // services customization for current test
                OnConfigureTestServices?.Invoke(services, Configuration!);
            });

            return base.CreateHost(builder);
        }

        public IHost CreateTestHost(string[]? args = null)
        {
            return Host.CreateDefaultBuilder(args)
                    .UseEnvironment("Test")
                    .ConfigureAppConfiguration(ConfigureAppConfiguration)
                    .ConfigureServices(ConfigureServices)
                    .Build();
        }

        private void ConfigureAppConfiguration(HostBuilderContext builderContext, IConfigurationBuilder builder)
        {
            var env = builderContext.HostingEnvironment.EnvironmentName;
            builder.AddJsonFile($"appsettings.{env}.json", optional: false)
                    .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            //services.AddApplicationInsightsTelemetryWorkerService(Configuration);

            var loggerConfiguration = new LoggerConfiguration()
                   .ReadFrom.Configuration(Configuration ?? throw new NullReferenceException($"{typeof(IConfiguration)} not implemented"));
            //.WriteTo.Console(LogEventLevel.Information, theme: AnsiConsoleTheme.Title);

            Log.Logger = loggerConfiguration.CreateLogger();

            services.Replace(ServiceDescriptor.Singleton(Log.Logger));

            services.AddOptions();
            services.AddAuthorization();
            services.AddRouting();
            services.AddControllers();

            services.ConfigureSettings(Configuration);
            services.ConfigureCache(Configuration!);
            services.ConfigureHttpClients(Configuration!);
            services.ConfigureHealthChecks(Configuration!);

            // services customization for current test
            //server?.ConfigureServices?.Invoke(services, configuration);

            var container = new ContainerBuilder();
            container.Populate(services);
            var serviceProvider = new AutofacServiceProvider(container.Build());
            services.AddSingleton<IServiceProvider>(serviceProvider);

            // build service's scope
            _serviceScope = services.BuildServiceProvider().CreateScope();
        }

        public void ConfigureServices(Action<IServiceCollection, IConfiguration?> action)
        {
            OnConfigureTestServices = action;
        }

        /// <summary> Get service from service provider </summary>
        public T GetService<T>()
        {
            if (_serviceScope == null)
                throw new ArgumentNullException($"serviceScope is null");

            return _serviceScope.ServiceProvider.GetService<T>()
                ?? throw new ArgumentNullException($"service {typeof(T)} not registed");
        }
    }
}