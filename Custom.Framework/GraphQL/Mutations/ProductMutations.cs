using Custom.Framework.GraphQL.Inputs;
using Custom.Framework.GraphQL.Models;
using Custom.Framework.GraphQL.Services;
using Custom.Framework.GraphQL.Subscriptions;
using HotChocolate;
using HotChocolate.Subscriptions;
using HotChocolate.Types;

namespace Custom.Framework.GraphQL.Mutations
{
    /// <summary>
    /// GraphQL mutations for products
    /// </summary>
    [ExtendObjectType(typeof(Mutation))]
    public class ProductMutations
    {
        /// <summary>
        /// Add a new product
        /// </summary>
        public async Task<Product> AddProduct(
            AddProductInput input,
            [Service] IProductService productService,
            [Service] ITopicEventSender eventSender)
        {
            var product = await productService.AddProductAsync(input);
            
            // Send event for subscription
            await eventSender.SendAsync(nameof(ProductSubscriptions.OnProductAdded), product);
            
            return product;
        }

        /// <summary>
        /// Update an existing product
        /// </summary>
        public async Task<Product?> UpdateProduct(
            UpdateProductInput input,
            [Service] IProductService productService,
            [Service] ITopicEventSender eventSender)
        {
            var product = await productService.UpdateProductAsync(input);
            
            if (product != null)
            {
                // Send event for subscription
                await eventSender.SendAsync(nameof(ProductSubscriptions.OnProductUpdated), product);
            }
            
            return product;
        }

        /// <summary>
        /// Delete a product
        /// </summary>
        public async Task<bool> DeleteProduct(
            int id,
            [Service] IProductService productService,
            [Service] ITopicEventSender eventSender)
        {
            var result = await productService.DeleteProductAsync(id);
            
            if (result)
            {
                // Send event for subscription
                await eventSender.SendAsync(nameof(ProductSubscriptions.OnProductDeleted), id);
            }
            
            return result;
        }
    }
}
