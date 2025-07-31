using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Custom.Framework.Telemetry
{
    /// <summary>
    /// ApiActivityFactory - interface for creating ActivitySource objects.
    /// </summary>
    public interface IApiActivityFactory
    {
        /// <summary>
        /// GetActivitySource - creating ActivitySource object.
        /// </summary>
        ActivitySource GetActivitySource(string name);

        /// <summary>
        ///  AddRequestCount - Request requestCount metric
        /// </summary>
        void AddRequestCount(int tick);

        /// <summary>
        ///  SetTotalMemoryHistogram - Total memory metric
        /// </summary>
        void SetTotalMemoryHistogram();
    }
}