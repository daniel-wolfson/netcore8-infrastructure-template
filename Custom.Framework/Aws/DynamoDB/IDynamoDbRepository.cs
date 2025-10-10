namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Interface for DynamoDB repository operations supporting high-load scenarios
/// </summary>
public interface IDynamoDbRepository<T> where T : class
{
    /// <summary>
    /// Get item by partition key
    /// </summary>
    Task<T?> GetAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get item by partition key and sort key
    /// </summary>
    Task<T?> GetAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Put/Update single item
    /// </summary>
    Task PutAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch write items (up to 25 items per batch)
    /// </summary>
    Task BatchWriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete item by partition key
    /// </summary>
    Task DeleteAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete item by partition key and sort key
    /// </summary>
    Task DeleteAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query items by partition key
    /// </summary>
    Task<IEnumerable<T>> QueryAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query items with filter expression
    /// </summary>
    Task<IEnumerable<T>> QueryAsync(
        string partitionKey,
        string filterExpression,
        Dictionary<string, object> expressionValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan table with optional filter
    /// </summary>
    Task<IEnumerable<T>> ScanAsync(
        string? filterExpression = null,
        Dictionary<string, object>? expressionValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch get items (up to 100 items per batch)
    /// </summary>
    Task<IEnumerable<T>> BatchGetAsync(
        IEnumerable<(string partitionKey, string? sortKey)> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update item with specific attributes
    /// </summary>
    Task UpdateAsync(
        string partitionKey,
        string sortKey,
        Dictionary<string, object> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conditional put - only put if condition is met
    /// </summary>
    Task<bool> ConditionalPutAsync(
        T item,
        string conditionExpression,
        Dictionary<string, object> expressionValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transactional write - all or nothing
    /// </summary>
    Task TransactWriteAsync(
        IEnumerable<T> itemsToPut,
        IEnumerable<(string partitionKey, string? sortKey)> keysToDelete,
        CancellationToken cancellationToken = default);
}
