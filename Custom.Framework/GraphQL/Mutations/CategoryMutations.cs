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
    /// GraphQL mutations for categories
    /// </summary>
    [ExtendObjectType(typeof(Mutation))]
    public class CategoryMutations
    {
        /// <summary>
        /// Add a new category
        /// </summary>
        public async Task<Category> AddCategory(
            AddCategoryInput input,
            [Service] ICategoryService categoryService,
            [Service] ITopicEventSender eventSender)
        {
            var category = await categoryService.AddCategoryAsync(input);
            
            // Send event for subscription
            await eventSender.SendAsync(nameof(CategorySubscriptions.OnCategoryAdded), category);
            
            return category;
        }

        /// <summary>
        /// Update an existing category
        /// </summary>
        public async Task<Category?> UpdateCategory(
            UpdateCategoryInput input,
            [Service] ICategoryService categoryService,
            [Service] ITopicEventSender eventSender)
        {
            var category = await categoryService.UpdateCategoryAsync(input);
            
            if (category != null)
            {
                // Send event for subscription
                await eventSender.SendAsync(nameof(CategorySubscriptions.OnCategoryUpdated), category);
            }
            
            return category;
        }

        /// <summary>
        /// Delete a category
        /// </summary>
        public async Task<bool> DeleteCategory(
            int id,
            [Service] ICategoryService categoryService,
            [Service] ITopicEventSender eventSender)
        {
            var result = await categoryService.DeleteCategoryAsync(id);
            
            if (result)
            {
                // Send event for subscription
                await eventSender.SendAsync(nameof(CategorySubscriptions.OnCategoryDeleted), id);
            }
            
            return result;
        }
    }
}
