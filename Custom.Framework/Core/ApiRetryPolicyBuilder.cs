using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Polly;
using Polly.Retry;
using Serilog;
using System.Net.Mail;

namespace Custom.Framework.Core
{
    public class ApiRetryPolicyBuilder : IRetryPolicyBuilder
    {
        private TimeSpan retryInterval = TimeSpan.FromSeconds(2);   // retryInterval, default 2 sec.
        private TimeSpan retryTimeout = TimeSpan.FromSeconds(5);    // retryTimeout, default 5 sec.
        private Func<int, TimeSpan>? retryWait;
        private readonly ILogger _logger;
        private CancellationTokenSource _cancelSource;
        private CancellationToken _cancelToken;

        public static readonly TimeSpan RetryIntervalDefault = TimeSpan.FromSeconds(2); // retryInterval by default 2 sec.
        public static readonly TimeSpan RetryTimeoutDefault = TimeSpan.FromSeconds(5);  // retryTimeout by default 5 sec.
        public int retryCount = 3;

        public ApiRetryPolicyBuilder(ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
            _cancelSource = new CancellationTokenSource();
            _cancelToken = _cancelSource.Token;
        }

        public static Func<int, TimeSpan> DefaultRetryWait => retryInterval => TimeSpan.FromSeconds(retryInterval * 2);

        #region public build methods

        /// <summary> add RetryCount to final build </summary>
        public ApiRetryPolicyBuilder WithRetryCount(int count)
        {
            if (count < 0)
                throw new ArgumentException("Retry count cannot be negative.");

            retryCount = count;
            return this;
        }

        /// <summary> add time interval between retries </summary>
        public ApiRetryPolicyBuilder WithRetryInterval(TimeSpan interval)
        {
            if (interval.TotalMilliseconds < 0)
                throw new ArgumentException("Retry interval cannot be negative.");

            if (retryTimeout.TotalSeconds < interval.TotalSeconds * retryCount)
            {
                // Count retry timeout must be not less to interval.TotalSeconds * retryCount * guardCount (for example, 2)
                WithRetryTimeout();
            }

            retryInterval = interval;
            return this;
        }

        /// <summary> add time-wait-strategy after each interval retries </summary>
        public ApiRetryPolicyBuilder WithRetryWait(Func<int, TimeSpan> retryWait)
        {
            this.retryWait = retryWait;
            return this;
        }

        /// <summary> add calculated timeout based on retryCount and retryWait strategy </summary>
        public ApiRetryPolicyBuilder WithRetryTimeout(TimeSpan? timeout = null)
        {
            TimeSpan total = retryInterval;
            for (int i = 0; i < retryCount; i++)
            {
                total = retryWait != null
                    ? retryWait((int)total.TotalSeconds)
                    : DefaultRetryWait((int)total.TotalSeconds);
            }

            if (timeout != null && timeout?.TotalSeconds >= total.TotalSeconds)
                retryTimeout = (TimeSpan)timeout;
            else
                retryTimeout = total;

            return this;
        }

        // Get AsyncRetryPolicy
        public static IAsyncPolicy<T> GetAsyncRetryPolicy<T>(int? retryCount = null, int? retryInterval = null)
        {
            var retryPolicy = new ApiRetryPolicyBuilder()
               .WithRetryCount(retryCount ?? 3)
               .WithRetryInterval(TimeSpan.FromSeconds(retryInterval ?? RetryTimeoutDefault.TotalSeconds)) //TimeSpan.FromSeconds(Math.Pow(retryInterval, retryCount))
               .WithRetryTimeout()
               .BuildAsyncPolicy<T>();
            return retryPolicy;
        }

        // Get SyncRetryPolicy
        public static ISyncPolicy<T> GetRetryPolicy<T>(int? retryCount = null, int? retryInterval = null)
        {
            var retryPolicy = new ApiRetryPolicyBuilder()
               .WithRetryCount(retryCount ?? 3)
               .WithRetryInterval(TimeSpan.FromSeconds(retryInterval ?? RetryTimeoutDefault.TotalSeconds)) //TimeSpan.FromSeconds(Math.Pow(retryInterval, retryCount))
               .WithRetryTimeout()
               .BuildSyncPolicy<T>();
            return retryPolicy;
        }

        //public async Task<AsyncRetryPolicy<TResult>> BuildAsyncPolicyAsync<TResult>()
        //{
        //    var d = await Policy
        //        .Handle<Exception>()
        //        .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(2), (ex, ts) => { Log.ErrorInfo("ErrorInfo sending mail. Retrying in 2 sec."); })
        //        .ExecuteAsync(() => OnRetry<TResult>());
        //        //.ContinueWith(_ => Log.Information("Notification mail sent to {Recipient}.", to));
        //}

        public AsyncRetryPolicy<TResult> BuildAsyncPolicy<TResult>()
        {
            return Policy<TResult>
                .Handle<Exception>() // Specify which exceptions to retry on (e.g., all exceptions)
                    .WaitAndRetryAsync(
                        retryCount,
                        retryInterval => retryWait != null ? retryWait(retryInterval) : DefaultRetryWait(retryInterval),
                        OnRetry<TResult>());
        }

        public RetryPolicy<TResult> BuildSyncPolicy<TResult>(Context? context = null)
        {
            return Policy<TResult>
                .Handle<Exception>() // Specify which exceptions to retry on (e.g., all exceptions)
                .WaitAndRetry(
                    retryCount,
                    retryInterval => TimeSpan.FromSeconds(Math.Pow(2, retryInterval)),
                    OnRetry<TResult>());
        }

        #endregion public build methods

        #region public operation methods

        // <summary> Retry and GetSettings </summary>
        public async Task<T?> RunAsync<T>(Func<Task<T>> onExecute, string? correlationId = null)
        {
            try
            {
                var retryTimeoutTask = Task.Delay(retryTimeout);
                var cancelSource = new CancellationTokenSource();
                var retryContext = MakeRetryContext(correlationId ?? Guid.NewGuid().ToString(), cancelSource);
                var retryExecuteTask = GetAsyncRetryPolicy<T>().ExecuteAsync((context, cancelToken) => onExecute(), retryContext, _cancelToken);
                var resultTask = Task.WhenAny(retryExecuteTask, retryTimeoutTask);

                return resultTask != retryTimeoutTask 
                    ? await retryExecuteTask 
                    : throw new TimeoutException("Retry policy timeout");
            }
            catch (Exception retryEx)
            {
                _logger.Error("Retry policy error: {Exception}. \nStackTrace: {StackTrace}\n", retryEx?.InnerException?.Message ?? retryEx?.Message, retryEx?.StackTrace);
                return default;
            }
        }

        // <summary> Retry and GetSettings </summary>
        public T? Run<T>(Func<T> onExecute, string? correlationId = null)
        {
            try
            {
                var cancelSource = new CancellationTokenSource();
                var cancelToken = _cancelSource.Token;
                var context = MakeRetryContext(correlationId ?? Guid.NewGuid().ToString(), cancelSource);

                var result = GetRetryPolicy<T>().Execute((context, cancelToken) =>
                    onExecute(), context, _cancelToken);

                return result;
            }
            catch (Exception retryEx)
            {
                _logger.Error("Retry policy error: {Exception}. \nStackTrace: {StackTrace}\n", retryEx?.InnerException?.Message ?? retryEx?.Message, retryEx?.StackTrace);
                return default;
            }
        }

        #endregion public operation methods

        #region private methods

        private Action<DelegateResult<TResult>, TimeSpan, int, Context> OnRetry<TResult>()
        {
            return (result, calculatedWaitDuration, retryCount, context) =>
            {
                if (retryCount == 1)
                    _logger.Warning("Retry policy starting due to {ExceptionType}.", result.Exception.GetType().Name);

                var correlationId = context.GetValueOrDefault(RequestHeaderKeys.CorrelationId) ?? context.CorrelationId;

                _logger.Warning("Retry #{retryCount} correlationId: {correlationId} for error {ExceptionType}, wait interval: {waitTimeToRetry} sec.",
                retryCount, correlationId, result.Exception.GetType().Name, calculatedWaitDuration.TotalSeconds);
            };
        }

        private Context MakeRetryContext(string correlationId, CancellationTokenSource? cancelSource = null)
        {
            var policyContext = new Context();
            var cts = cancelSource ?? new CancellationTokenSource();
            policyContext.Add("CancelSource", cts);
            policyContext.Add("CancelToken", cts.Token);
            policyContext.Add("CorrelationId", correlationId);
            return policyContext;
        }

        #endregion private methods
    }
}