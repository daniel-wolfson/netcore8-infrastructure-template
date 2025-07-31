namespace Custom.Framework.StaticData.Confiuration
{
    public class RequestMiddlewareOptions
    {
        public CancellationToken StaticDataCancellationToken { get; set; }

        public CancellationTokenSource StaticDataCancellationSource { get; set; } = default!;
    }
}