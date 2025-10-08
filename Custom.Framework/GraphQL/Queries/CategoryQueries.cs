using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.Queries
{
    /// <summary>
    /// GraphQL queries for categories
    /// </summary>
    [ExtendObjectType(typeof(Query))]
    public class CategoryQueries
    {
        /// <summary>
        /// Get all categories with filtering, sorting, and projection support
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        [UseProjection]
        [HotChocolate.Data.UseFiltering]
        [UseSorting]
        public IQueryable<Category> GetCategories([FromServices] ApplicationDbContext context)
        {
            return context.Categories.Include(c => c.Products);
        }

        /// <summary>
        /// Get a category by its ID
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        public async Task<Category?> GetCategoryById(
            int id,
            [FromServices] ApplicationDbContext context)
        {
            return await context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>
        /// Get category by name
        /// </summary>
       // [UseDbContext(typeof(ApplicationDbContext))]
        public async Task<Category?> GetCategoryByName(
            string name,
            [FromServices] ApplicationDbContext context)
        {
            return await context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Name == name);
        }
    }
}
