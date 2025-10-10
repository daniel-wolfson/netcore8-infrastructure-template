using Custom.Framework.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Custom.Framework.TestFactory.Core;

public class TestHostStartup(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;

    public void ConfigureAppConfiguration(ConfigurationManager configuration, IWebHostEnvironment env)
    {
    }

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.Run(async context =>
        {
            await context.Response.WriteAsync("Hello from TestHostStartup!");
        });
    }
}

