namespace Custom.Framework.Middleware
{
    public class RequestMiddlewareOptions_
    {
        public CancellationToken StaticDataCancellationToken { get; set; }

        public CancellationTokenSource StaticDataCancellationSource { get; set; }
    }
}