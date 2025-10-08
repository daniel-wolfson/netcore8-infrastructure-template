using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.Queries
{
    /// <summary>
    /// GraphQL queries for products
    /// </summary>
    [ExtendObjectType(typeof(Query))]
    public class ProductQueries
    {
        /// <summary>
        /// Get all products with filtering, sorting, and projection support
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        [UseProjection]
        [HotChocolate.Data.UseFiltering]
        [UseSorting]
        public IQueryable<Product> GetProducts([FromServices] ApplicationDbContext context)
        {
            return context.Products.Include(p => p.Category);
        }

        /// <summary>
        /// Get a product by its ID
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        public async Task<Product?> GetProductById(
            int id,
            [FromServices] ApplicationDbContext context)
        {
            return await context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// Get products by category ID
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        [UseProjection]
        [HotChocolate.Data.UseFiltering]
        [UseSorting]
        public IQueryable<Product> GetProductsByCategory(
            int categoryId,
            [FromServices] ApplicationDbContext context)
        {
            return context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId);
        }

        /// <summary>
        /// Search products by name
        /// </summary>
        //[UseDbContext(typeof(ApplicationDbContext))]
        public async Task<IEnumerable<Product>> SearchProducts(
            string searchTerm,
            [FromServices] ApplicationDbContext context)
        {
            return await context.Products
                .Include(p => p.Category)
                .Where(p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm))
                .ToListAsync();
        }
    }
}
