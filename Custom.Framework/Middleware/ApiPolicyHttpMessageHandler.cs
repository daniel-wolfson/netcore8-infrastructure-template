// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Isrotel.Framework.Middleware;

public class ApiPolicyHttpMessageHandler : DelegatingHandler
{
    private const string PriorResponseKey = "PolicyHttpMessageHandler.PriorResponse";
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;
    private readonly Func<HttpRequestMessage, IAsyncPolicy<HttpResponseMessage>> _policySelector;

    public ApiPolicyHttpMessageHandler(IAsyncPolicy<HttpResponseMessage> policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        _policy = policy;
    }

    public ApiPolicyHttpMessageHandler(Func<HttpRequestMessage, IAsyncPolicy<HttpResponseMessage>> policySelector)
    {
        if (policySelector == null)
        {
            throw new ArgumentNullException(nameof(policySelector));
        }

        _policySelector = policySelector;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Guarantee the existence of a context for every policy execution, but only create a new one if needed. This
        // allows later handlers to flow state if desired.
        var cleanUpContext = false;
        var context = request.GetPolicyExecutionContext();
        if (context == null)
        {
            context = new Context();
            request.SetPolicyExecutionContext(context);
            cleanUpContext = true;
        }

        HttpResponseMessage response;
        try
        {
            var policy = _policy ?? SelectPolicy(request);
            response = await policy.ExecuteAsync((c, ct) => SendCoreAsync(request, c, ct), context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (cleanUpContext)
            {
                request.SetPolicyExecutionContext(null);
            }
        }

        return response;
    }

    /// <summary>
    /// Called inside the execution of the <see cref="Policy"/> to perform request processing.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/>.</param>
    /// <param name="context">The <see cref="Context"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Returns a <see cref="Task{HttpResponseMessage}"/> that will yield a response when completed.</returns>
    protected virtual async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, Context context, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var disposable = request.Options.GetValueOrDefault(PriorResponseKey);
        if (disposable != null)
        {
            // TODO: Need Properties to Options
            //.Properties.TryGetValue(PriorResponseKey, out var priorResult) && priorResult is IDisposable disposable)
            // This is a retry, dispose the prior response to free up the connection.
            //request.Options.Remove(PriorResponseKey);
            //disposable.Dispose();
        }

        var result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        request.Options.TryAdd(PriorResponseKey, result);

        return result;
    }

    private IAsyncPolicy<HttpResponseMessage> SelectPolicy(HttpRequestMessage request)
    {
        var policy = _policySelector(request);
        if (policy == null)
        {
            throw new InvalidOperationException("policySelector is null");
        }

        return policy;
    }
}