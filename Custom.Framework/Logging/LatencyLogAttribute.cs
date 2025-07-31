using Custom.Framework.Exceptions;
using Custom.Framework.Logging;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

[Serializable]
public class LatencyLogAttribute(IHttpContextAccessor httpContextAccessor) //: OnMethodBoundaryAspect
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger _logger;
    private ILogger _latencyLog;

    protected ILogger LatencyLog => _latencyLog ??= GetService<ILogger>(typeof(ApiActivityLogger).Name)!;
    public HttpContext? HttpContext1 { get; init; } = httpContextAccessor.HttpContext;

    //public override void OnEntry(MethodExecutionArgs args)
    //{
    //    ActivityTrace.Add($"{args.Method.FilterName}:Entry");
    //}

    //public override void OnExit(MethodExecutionArgs args)
    //{
    //    ActivityTrace.Add($"{args.Method.FilterName}:Exit");
    //}

    /// <summary> GetService go to get the instance of TFilterType from registered services </summary>
    protected T GetService<T>(string? serviceKey = null)
    {
        if (HttpContext1 == null)
            throw new ApiException(ServiceStatus.FatalError, "HttpContext not defined");

        if (!string.IsNullOrEmpty(serviceKey))
            return HttpContext1.RequestServices.GetKeyedService<T>(serviceKey)!;

        var service = HttpContext1.RequestServices.GetService(typeof(T))
            ?? throw new ApiException(ServiceStatus.FatalError, $"{nameof(GetService)} error: {typeof(T).Name} not registered");

        return (T)service;
    }
}


