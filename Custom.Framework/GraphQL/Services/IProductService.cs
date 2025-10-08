using Custom.Framework.GraphQL.Inputs;
using Custom.Framework.GraphQL.Models;

namespace Custom.Framework.GraphQL.Services
{
    /// <summary>
    /// Service interface for product operations
    /// </summary>
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product> AddProductAsync(AddProductInput input);
        Task<Product?> UpdateProductAsync(UpdateProductInput input);
        Task<bool> DeleteProductAsync(int id);
    }
}
