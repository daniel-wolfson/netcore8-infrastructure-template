using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Inputs;
using Custom.Framework.GraphQL.Models;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.Services
{
    /// <summary>
    /// Service implementation for product operations
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;

        public ProductService(ApplicationDbContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            try
            {
                return await _context.Products
                    .Include(p => p.Category)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting products");
                throw;
            }
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            try
            {
                return await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting product by id {ProductId}", id);
                throw;
            }
        }

        public async Task<Product> AddProductAsync(AddProductInput input)
        {
            try
            {
                var product = new Product
                {
                    Name = input.Name,
                    Description = input.Description,
                    Price = input.Price,
                    CategoryId = input.CategoryId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                
                _logger.Information("Product created: {ProductId} - {ProductName}", product.Id, product.Name);
                
                return await GetProductByIdAsync(product.Id) ?? product;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adding product {ProductName}", input.Name);
                throw;
            }
        }

        public async Task<Product?> UpdateProductAsync(UpdateProductInput input)
        {
            try
            {
                var product = await _context.Products.FindAsync(input.Id);
                if (product == null)
                {
                    _logger.Warning("Product not found: {ProductId}", input.Id);
                    return null;
                }

                if (input.Name != null) product.Name = input.Name;
                if (input.Description != null) product.Description = input.Description;
                if (input.Price.HasValue) product.Price = input.Price.Value;
                if (input.CategoryId.HasValue) product.CategoryId = input.CategoryId.Value;
                
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.Information("Product updated: {ProductId} - {ProductName}", product.Id, product.Name);
                
                return await GetProductByIdAsync(product.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating product {ProductId}", input.Id);
                throw;
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _logger.Warning("Product not found for deletion: {ProductId}", id);
                    return false;
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                
                _logger.Information("Product deleted: {ProductId} - {ProductName}", id, product.Name);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting product {ProductId}", id);
                throw;
            }
        }
    }
}
