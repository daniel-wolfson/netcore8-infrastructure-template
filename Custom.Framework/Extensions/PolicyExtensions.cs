using Polly;
using Polly.Extensions.Http;

namespace Custom.Framework.Extensions
{
    public class PolicyExtensions
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount = 3)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }
    }
}
