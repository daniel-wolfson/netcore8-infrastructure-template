using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Models;
using GreenDonut;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.DataLoaders
{
    /// <summary>
    /// DataLoader for efficiently loading categories (solves N+1 problem)
    /// </summary>
    public class CategoryDataLoader : BatchDataLoader<int, Category>
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public CategoryDataLoader(
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        protected override async Task<IReadOnlyDictionary<int, Category>> LoadBatchAsync(
            IReadOnlyList<int> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var categories = await context.Categories
                .Where(c => keys.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);
                
            return categories;
        }
    }
}
