using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Generic repository implementation for Aurora database
/// Provides high-performance CRUD operations with read/write splitting
/// </summary>
public class AuroraRepository<T> : IAuroraRepository<T> where T : class
{
    protected readonly AuroraDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger<AuroraRepository<T>> _logger;
    protected readonly AuroraDbOptions _options;

    public AuroraRepository(
        AuroraDbContext context,
        ILogger<AuroraRepository<T>> logger,
        AuroraDbOptions options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbSet = _context.Set<T>();
    }

    #region Read Operations

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            _logger.LogDebug("GetByIdAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {EntityType} by id {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            _logger.LogDebug("GetByIdAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {EntityType} by id {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            _logger.LogDebug("GetByIdAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {EntityType} by id {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
            _logger.LogDebug("FirstOrDefaultAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FirstOrDefaultAsync for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var results = await _dbSet.ToListAsync(cancellationToken);
            _logger.LogDebug("GetAllAsync<{EntityType}> returned {Count} items in {ElapsedMs}ms",
                typeof(T).Name, results.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var results = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
            _logger.LogDebug("FindAsync<{EntityType}> returned {Count} items in {ElapsedMs}ms",
                typeof(T).Name, results.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FindAsync for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _dbSet.AnyAsync(predicate, cancellationToken);
            _logger.LogDebug("AnyAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnyAsync for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var count = predicate == null
                ? await _dbSet.CountAsync(cancellationToken)
                : await _dbSet.CountAsync(predicate, cancellationToken);

            _logger.LogDebug("CountAsync<{EntityType}> returned {Count} in {ElapsedMs}ms",
                typeof(T).Name, count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CountAsync for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual IQueryable<T> GetQueryable(bool useReadReplica = false)
    {
        if (useReadReplica && _options.EnableReadReplicas && !string.IsNullOrEmpty(_options.ReadEndpoint))
        {
            var replicaContext = _context.CreateReadReplicaContext();
            return replicaContext.Set<T>().AsNoTracking();
        }

        return _dbSet.AsQueryable();
    }

    #endregion

    #region Write Operations

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            await _dbSet.AddAsync(entity, cancellationToken);
            _logger.LogDebug("AddAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var entitiesList = entities.ToList();
        try
        {
            await _dbSet.AddRangeAsync(entitiesList, cancellationToken);
            _logger.LogDebug("AddRangeAsync<{EntityType}> added {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entitiesList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding range of {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            _dbSet.Update(entity);
            _logger.LogDebug("UpdateAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var entitiesList = entities.ToList();
        try
        {
            _dbSet.UpdateRange(entitiesList);
            _logger.LogDebug("UpdateRangeAsync<{EntityType}> updated {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entitiesList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating range of {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            _dbSet.Remove(entity);
            _logger.LogDebug("DeleteAsync<{EntityType}> completed in {ElapsedMs}ms",
                typeof(T).Name, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var entitiesList = entities.ToList();
        try
        {
            _dbSet.RemoveRange(entitiesList);
            _logger.LogDebug("DeleteRangeAsync<{EntityType}> deleted {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entitiesList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting range of {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    public virtual async Task DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    public virtual async Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    #endregion

    #region Bulk Operations

    public virtual async Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var entitiesList = entities.ToList();
        try
        {
            await _dbSet.AddRangeAsync(entitiesList, cancellationToken);
            var result = await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("BulkInsertAsync<{EntityType}> inserted {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entitiesList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk insert for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<int> BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var entitiesList = entities.ToList();
        try
        {
            _dbSet.UpdateRange(entitiesList);
            var result = await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("BulkUpdateAsync<{EntityType}> updated {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entitiesList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk update for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
            _dbSet.RemoveRange(entities);
            var result = await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("BulkDeleteAsync<{EntityType}> deleted {Count} items in {ElapsedMs}ms",
                typeof(T).Name, entities.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk delete for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    #endregion

    #region Transaction Support

    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("SaveChangesAsync completed with {Changes} changes in {ElapsedMs}ms",
                result, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes");
            throw;
        }
    }

    public virtual async Task<TResult> ExecuteInTransactionAsync<TResult>(
    Func<Task<TResult>> operation,
    CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();

        // Create execution strategy
        var strategy = _context.Database.CreateExecutionStrategy();

        // Execute full transaction inside the strategy
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug("Transaction completed successfully in {ElapsedMs}ms",
                    Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back due to error");
                throw;
            }
        });
    }

    #endregion

    #region Raw SQL

    public virtual async Task<List<T>> ExecuteSqlQueryAsync(string sql, params object[] parameters)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var results = await Microsoft.EntityFrameworkCore.RelationalQueryableExtensions
                .FromSqlRaw(_dbSet, sql, parameters)
                .ToListAsync();
            _logger.LogDebug("ExecuteSqlQueryAsync returned {Count} items in {ElapsedMs}ms",
                results.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query");
            throw;
        }
    }

    public virtual async Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
            _logger.LogDebug("ExecuteSqlCommandAsync affected {Rows} rows in {ElapsedMs}ms",
                result, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL command");
            throw;
        }
    }

    #endregion
}
