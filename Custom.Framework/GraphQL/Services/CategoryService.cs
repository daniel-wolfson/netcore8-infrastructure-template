using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Inputs;
using Custom.Framework.GraphQL.Models;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.Services
{
    /// <summary>
    /// Service implementation for category operations
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;

        public CategoryService(ApplicationDbContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync()
        {
            try
            {
                return await _context.Categories
                    .Include(c => c.Products)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting categories");
                throw;
            }
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            try
            {
                return await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting category by id {CategoryId}", id);
                throw;
            }
        }

        public async Task<Category> AddCategoryAsync(AddCategoryInput input)
        {
            try
            {
                var category = new Category
                {
                    Name = input.Name,
                    Description = input.Description
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                
                _logger.Information("Category created: {CategoryId} - {CategoryName}", category.Id, category.Name);
                
                return await GetCategoryByIdAsync(category.Id) ?? category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adding category {CategoryName}", input.Name);
                throw;
            }
        }

        public async Task<Category?> UpdateCategoryAsync(UpdateCategoryInput input)
        {
            try
            {
                var category = await _context.Categories.FindAsync(input.Id);
                if (category == null)
                {
                    _logger.Warning("Category not found: {CategoryId}", input.Id);
                    return null;
                }

                if (input.Name != null) category.Name = input.Name;
                if (input.Description != null) category.Description = input.Description;

                await _context.SaveChangesAsync();
                
                _logger.Information("Category updated: {CategoryId} - {CategoryName}", category.Id, category.Name);
                
                return await GetCategoryByIdAsync(category.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating category {CategoryId}", input.Id);
                throw;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);
                
                if (category == null)
                {
                    _logger.Warning("Category not found for deletion: {CategoryId}", id);
                    return false;
                }

                if (category.Products.Any())
                {
                    _logger.Warning("Cannot delete category with products: {CategoryId}", id);
                    throw new InvalidOperationException($"Cannot delete category '{category.Name}' because it contains products.");
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                
                _logger.Information("Category deleted: {CategoryId} - {CategoryName}", id, category.Name);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting category {CategoryId}", id);
                throw;
            }
        }
    }
}
