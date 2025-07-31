using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net;

namespace Custom.Framework.TestFactory.Core;

public static class TestGeneralContext
{
    #region ServiceProvider

    public static IServiceProvider? ServiceProvider { get; set; }
    public static IServiceScope? ServiceScope { get; set; }

    public static IServiceScope CreateServiceScope()
    {
        return ServiceProvider!.GetRequiredService<IServiceScopeFactory>().CreateScope();
    }

    public static TService GetService<TService>()
    {
        TService? serviceResult = default;

        try
        {
            if (ServiceScope != null)
                serviceResult = (TService)HttpContext.RequestServices.GetService(typeof(TService))!;
            else
                serviceResult = (TService)ServiceProvider?.GetService(typeof(TService))!;

            if (serviceResult == null)
                throw new NotImplementedException("service not implemented");
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, ex.GetApiMessageInfo());
        }

        return serviceResult!;
    }

    public static object GetService(Type serviceType)
    {
        object? serviceResult = null;

        try
        {
            if (ServiceScope != null)
                serviceResult = HttpContext.RequestServices.GetService(serviceType)!;
            else
                serviceResult = ServiceProvider?.GetService(serviceType)!;

            if (serviceResult == null)
                throw new NotImplementedException("service not implemented");
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, ex.GetApiMessageInfo());
        }

        return serviceResult!;
    }

    public static void SetServiceProvider(IServiceProvider services)
    {
        ServiceProvider = services;

        ServicePointManager.ServerCertificateValidationCallback +=
            delegate (object sender,
                System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
                System.Security.Cryptography.X509Certificates.X509Chain? chain,
                System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                return true; // **** Always accept
            };
    }

    #endregion ServiceProvider

    #region HttpContext & HttpCliens

    // TraceIdentifier preventing calling twice
    public static string? RequestTraceId { get; set; }

    /// <summary> Provides static access to the current HttpContext /// </summary>
    private static HttpContext? _httpContext;
    public static HttpContext HttpContext
    {
        get
        {
            return _httpContext ?? ServiceProvider?.GetService<IHttpContextAccessor>()!.HttpContext!;
        }
        set
        {
            _httpContext = value;
        }
    }

    public static string CreateTraceId()
    {
        return Guid.NewGuid().ToString("n").Substring(0, 8);
    }

    #endregion HttpContext & HttpCliens
}