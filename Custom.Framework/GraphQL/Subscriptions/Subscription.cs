using Custom.Framework.GraphQL.Models;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace Custom.Framework.GraphQL.Subscriptions
{
    /// <summary>
    /// Base subscription class for GraphQL
    /// Extended by specific subscription classes for real-time features
    /// </summary>
    public class Subscription
    {
        //[Subscribe]
        //public async ValueTask<ISourceStream<StockQuote>> OnStockPriceUpdated(
        //[Service] ITopicEventReceiver eventReceiver,
        //CancellationToken cancellationToken)
        //{
        //    return await eventReceiver.SubscribeAsync<StockQuote>("StockPriceUpdated", cancellationToken);
        //}
    }
}
