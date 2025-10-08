using Custom.Framework.GraphQL.Inputs;
using Custom.Framework.GraphQL.Models;

namespace Custom.Framework.GraphQL.Services
{
    /// <summary>
    /// Service interface for category operations
    /// </summary>
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetCategoriesAsync();
        Task<Category?> GetCategoryByIdAsync(int id);
        Task<Category> AddCategoryAsync(AddCategoryInput input);
        Task<Category?> UpdateCategoryAsync(UpdateCategoryInput input);
        Task<bool> DeleteCategoryAsync(int id);
    }
}
