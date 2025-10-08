using Custom.Framework.GraphQL.Models;
using HotChocolate;
using HotChocolate.Types;

namespace Custom.Framework.GraphQL.Subscriptions
{
    /// <summary>
    /// GraphQL subscriptions for real-time product updates
    /// </summary>
    [ExtendObjectType(typeof(Subscription))]
    public class ProductSubscriptions
    {
        /// <summary>
        /// Subscribe to product additions
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnProductAdded))]
        public Product OnProductAdded([EventMessage] Product product) => product;

        /// <summary>
        /// Subscribe to product updates
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnProductUpdated))]
        public Product OnProductUpdated([EventMessage] Product product) => product;

        /// <summary>
        /// Subscribe to product deletions
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnProductDeleted))]
        public int OnProductDeleted([EventMessage] int productId) => productId;
    }
}
