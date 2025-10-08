using Custom.Framework.GraphQL.Models;
using HotChocolate;
using HotChocolate.Types;

namespace Custom.Framework.GraphQL.Subscriptions
{
    /// <summary>
    /// GraphQL subscriptions for real-time category updates
    /// </summary>
    [ExtendObjectType(typeof(Subscription))]
    public class CategorySubscriptions
    {
        /// <summary>
        /// Subscribe to category additions
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnCategoryAdded))]
        public Category OnCategoryAdded([EventMessage] Category category) => category;

        /// <summary>
        /// Subscribe to category updates
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnCategoryUpdated))]
        public Category OnCategoryUpdated([EventMessage] Category category) => category;

        /// <summary>
        /// Subscribe to category deletions
        /// </summary>
        [Subscribe]
        [Topic(nameof(OnCategoryDeleted))]
        public int OnCategoryDeleted([EventMessage] int categoryId) => categoryId;
    }
}
