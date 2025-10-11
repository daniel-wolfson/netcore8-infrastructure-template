using System.Linq.Expressions;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Generic repository interface for Aurora database operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IAuroraRepository<T> where T : class
{
    // Read Operations
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    // Queryable for complex queries
    IQueryable<T> GetQueryable(bool useReadReplica = false);

    // Write Operations
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default);

    // Bulk Operations
    Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<int> BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    // Transaction Support
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default);

    // Raw SQL
    Task<List<T>> ExecuteSqlQueryAsync(string sql, params object[] parameters);
    Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters);
}
