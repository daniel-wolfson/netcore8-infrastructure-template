using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.HealthChecks;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Mime;

namespace Custom.Framework.HealthChecks;
public static class HealthCheckExtensions
{
    public static IApplicationBuilder UseHealthChecks<T>(this IApplicationBuilder app, PathString path)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseHealthChecks(path, new HealthCheckOptions() //"/healthcheck"
        {
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/version", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("Version"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/redis", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("Redis"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/dalapi", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("DalApi"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/azureblob", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("AzureBlob"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/optimaapi", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("OptimaBaseRepository"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks($"{path}/apisettings", new HealthCheckOptions()
        {
            Predicate = (check) => check.Tags.Contains("apiSettings"),
            ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
        });
        app.UseHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        var config = app.ApplicationServices.GetService(typeof(IConfiguration)) as IConfiguration;
        var apiSettings = config?
            .GetSections(SettingKeys.StaticData.ToString())
            .Select(x => x.SettingKey)
            .ToList();

        apiSettings?.ForEach(apiSetting =>
        {
            app.UseHealthChecks($"{path}/{apiSetting}", new HealthCheckOptions()
            {
                Predicate = (check) => check.Tags.Contains(apiSetting.ToString()),
                ResponseWriter = ApiHealthCheckResponseWriter.Write<T>
            });
        });

        return app;
    }

    //TODO: temprary, need for analysis and remove
    public static IEndpointConventionBuilder MapCustomHealthChecks(
        this IEndpointRouteBuilder endpoint,
        string endpointUrl,
        string checkName,
        string tagToFilter = ""
    )
    {
        var endpointConventionBuilder = endpoint.MapHealthChecks(
            endpointUrl,
            new HealthCheckOptions
            {
                Predicate = GetFilter(tagToFilter),
                ResponseWriter = async (context, report) =>
                {
                    string json = ApiJsonHelper.ToJson(report, checkName);
                    context.Response.ContentType = MediaTypeNames.Application.Json;
                    await context.Response.WriteAsync(json);
                }
            }
        );

        return endpointConventionBuilder;
    }

    private static Func<HealthCheckRegistration, bool> GetFilter(string tag)
    {
        Func<HealthCheckRegistration, bool> filterPredicate =
            filterPredicate = check => check.Tags.Any(t => t == tag);

        if (string.IsNullOrWhiteSpace(tag)) filterPredicate = x => true;

        return filterPredicate;
    }


}