using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;

namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// High-performance DynamoDB repository implementation
/// Optimized for high-load read/write scenarios
/// </summary>
public class DynamoDbRepository<T> : IDynamoDbRepository<T>, IDisposable where T : class
{
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbOptions _options;
    private readonly DynamoDBContext _context;
    private readonly ILogger<DynamoDbRepository<T>> _logger;
    private readonly string _tableName;
    private bool _disposed;

    public DynamoDbRepository(
        IAmazonDynamoDB client,
        IOptions<DynamoDbOptions> options,
        ILogger<DynamoDbRepository<T>> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _context = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => _client)
            .ConfigureContext(config =>
            {
                config.ConsistentRead = true;
                config.SkipVersionCheck = true;
            })
            .Build();

        var tableAttribute = typeof(T).GetCustomAttributes(typeof(DynamoDBTableAttribute), true)
            .FirstOrDefault() as DynamoDBTableAttribute;

        _tableName = tableAttribute?.TableName ?? _options.TableName ?? typeof(T).Name;
    }

    public async Task<T?> GetAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _context.LoadAsync<T>(partitionKey, cancellationToken);
            _logger.LogDebug("GetAsync completed in {ElapsedMs}ms for key {Key}", 
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, partitionKey);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item with key {Key}", partitionKey);
            throw;
        }
    }

    public async Task<T?> GetAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await _context.LoadAsync<T>(partitionKey, sortKey, cancellationToken);
            _logger.LogDebug("GetAsync completed in {ElapsedMs}ms for keys {PartitionKey}/{SortKey}",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, partitionKey, sortKey);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item with keys {PartitionKey}/{SortKey}", partitionKey, sortKey);
            throw;
        }
    }

    public async Task PutAsync(T item, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            await _context.SaveAsync(item, cancellationToken);
            _logger.LogDebug("PutAsync completed in {ElapsedMs}ms", Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting item");
            throw;
        }
    }

    public async Task BatchWriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var itemsList = items.ToList();

        if (!itemsList.Any())
        {
            _logger.LogWarning("BatchWriteAsync called with empty items collection");
            return;
        }

        try
        {
            var batches = itemsList.Chunk(_options.MaxBatchSize);
            var batchCount = 0;

            foreach (var batch in batches)
            {
                var batchWrite = _context.CreateBatchWrite<T>();
                foreach (var item in batch)
                {
                    batchWrite.AddPutItem(item);
                }

                await batchWrite.ExecuteAsync(cancellationToken);
                batchCount++;
                _logger.LogDebug("Batch {BatchNumber} completed with {ItemCount} items", batchCount, batch.Length);
            }

            _logger.LogInformation("BatchWriteAsync completed {BatchCount} batches with {TotalItems} items in {ElapsedMs}ms",
                batchCount, itemsList.Count, Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch write operation");
            throw;
        }
    }

    public async Task DeleteAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            await _context.DeleteAsync<T>(partitionKey, cancellationToken);
            _logger.LogDebug("DeleteAsync completed in {ElapsedMs}ms for key {Key}", 
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, partitionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item with key {Key}", partitionKey);
            throw;
        }
    }

    public async Task DeleteAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            await _context.DeleteAsync<T>(partitionKey, sortKey, cancellationToken);
            _logger.LogDebug("DeleteAsync completed in {ElapsedMs}ms for keys {PartitionKey}/{SortKey}",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, partitionKey, sortKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item with keys {PartitionKey}/{SortKey}", partitionKey, sortKey);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var search = _context.QueryAsync<T>(partitionKey);
            var results = await search.GetRemainingAsync(cancellationToken);

            _logger.LogDebug("QueryAsync completed in {ElapsedMs}ms, returned {Count} items",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying items with partition key {Key}", partitionKey);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync(
        string partitionKey,
        string filterExpression,
        Dictionary<string, object> expressionValues,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            // Use low-level client for custom queries
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = filterExpression,
                ExpressionAttributeValues = expressionValues.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue { S = kvp.Value?.ToString() ?? "" })
            };

            var response = await _client.QueryAsync(request, cancellationToken);
            var results = new List<T>();

            foreach (var item in response.Items)
            {
                var doc = Amazon.DynamoDBv2.DocumentModel.Document.FromAttributeMap(item);
                var obj = _context.FromDocument<T>(doc);
                results.Add(obj);
            }

            _logger.LogDebug("QueryAsync with filter completed in {ElapsedMs}ms, returned {Count} items",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying items with filter");
            throw;
        }
    }

    public async Task<IEnumerable<T>> ScanAsync(
        string? filterExpression = null,
        Dictionary<string, object>? expressionValues = null,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var results = new List<T>();

            if (!string.IsNullOrEmpty(filterExpression) && expressionValues != null)
            {
                // Use low-level client for custom scan
                var request = new ScanRequest
                {
                    TableName = _tableName,
                    FilterExpression = filterExpression,
                    ExpressionAttributeValues = expressionValues.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new AttributeValue { S = kvp.Value?.ToString() ?? "" })
                };

                var response = await _client.ScanAsync(request, cancellationToken);
                
                foreach (var item in response.Items)
                {
                    var doc = Amazon.DynamoDBv2.DocumentModel.Document.FromAttributeMap(item);
                    var obj = _context.FromDocument<T>(doc);
                    results.Add(obj);
                }
            }
            else
            {
                var search = _context.ScanAsync<T>(default(List<Amazon.DynamoDBv2.DataModel.ScanCondition>));
                results = await search.GetRemainingAsync(cancellationToken);
            }

            _logger.LogDebug("ScanAsync completed in {ElapsedMs}ms, returned {Count} items",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning table");
            throw;
        }
    }

    public async Task<IEnumerable<T>> BatchGetAsync(
        IEnumerable<(string partitionKey, string? sortKey)> keys,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        var keysList = keys.ToList();

        if (!keysList.Any())
        {
            _logger.LogWarning("BatchGetAsync called with empty keys collection");
            return Enumerable.Empty<T>();
        }

        try
        {
            var batchGet = _context.CreateBatchGet<T>();

            foreach (var (partitionKey, sortKey) in keysList)
            {
                if (string.IsNullOrEmpty(sortKey))
                {
                    batchGet.AddKey(partitionKey);
                }
                else
                {
                    batchGet.AddKey(partitionKey, sortKey);
                }
            }

            await batchGet.ExecuteAsync(cancellationToken);
            var results = batchGet.Results;

            _logger.LogDebug("BatchGetAsync completed in {ElapsedMs}ms, requested {RequestCount} items, returned {ResultCount} items",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, keysList.Count, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch get operation");
            throw;
        }
    }

    public async Task UpdateAsync(
        string partitionKey,
        string sortKey,
        Dictionary<string, object> updates,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var updateExpression = "SET " + string.Join(", ", updates.Select((kvp, i) => $"#{kvp.Key} = :val{i}"));
            var expressionAttributeNames = updates.ToDictionary(kvp => $"#{kvp.Key}", kvp => kvp.Key);
            var expressionAttributeValues = updates.Select((kvp, i) => new { Key = $":val{i}", Value = kvp.Value })
                .ToDictionary(x => x.Key, x => new AttributeValue { S = x.Value?.ToString() ?? "" });

            var key = BuildKeyAttributes(partitionKey, sortKey);

            var request = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = key,
                UpdateExpression = updateExpression,
                ExpressionAttributeNames = expressionAttributeNames,
                ExpressionAttributeValues = expressionAttributeValues
            };

            await _client.UpdateItemAsync(request, cancellationToken);

            _logger.LogDebug("UpdateAsync completed in {ElapsedMs}ms for keys {PartitionKey}/{SortKey}",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, partitionKey, sortKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item with keys {PartitionKey}/{SortKey}", partitionKey, sortKey);
            throw;
        }
    }

    public async Task<bool> ConditionalPutAsync(
        T item,
        string conditionExpression,
        Dictionary<string, object> expressionValues,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            // Use low-level client for conditional writes
            var document = _context.ToDocument(item);
            var attributeMap = document.ToAttributeMap();

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = attributeMap,
                ConditionExpression = conditionExpression,
                ExpressionAttributeValues = expressionValues.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue { S = kvp.Value?.ToString() ?? "" })
            };

            await _client.PutItemAsync(request, cancellationToken);

            _logger.LogDebug("ConditionalPutAsync completed in {ElapsedMs}ms", Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds);
            return true;
        }
        catch (ConditionalCheckFailedException ex)
        {
            _logger.LogWarning(ex, "Conditional check failed for put operation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in conditional put operation");
            throw;
        }
    }

    public async Task TransactWriteAsync(
        IEnumerable<T> itemsToPut,
        IEnumerable<(string partitionKey, string? sortKey)> keysToDelete,
        CancellationToken cancellationToken = default)
    {
        var startedTime = Stopwatch.GetTimestamp();
        try
        {
            var putItems = itemsToPut.ToList();
            var deleteKeys = keysToDelete.ToList();

            var transactItems = new List<TransactWriteItem>();

            foreach (var item in putItems)
            {
                var document = _context.ToDocument(item);
                transactItems.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = document.ToAttributeMap()
                    }
                });
            }

            foreach (var (partitionKey, sortKey) in deleteKeys)
            {
                var key = BuildKeyAttributes(partitionKey, sortKey);

                transactItems.Add(new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _tableName,
                        Key = key
                    }
                });
            }

            var request = new TransactWriteItemsRequest
            {
                TransactItems = transactItems
            };

            await _client.TransactWriteItemsAsync(request, cancellationToken);

            _logger.LogInformation("TransactWriteAsync completed in {ElapsedMs}ms with {PutCount} puts and {DeleteCount} deletes",
                Stopwatch.GetElapsedTime(startedTime).TotalMilliseconds, putItems.Count, deleteKeys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in transactional write operation");
            throw;
        }
    }

    private static Dictionary<string, AttributeValue> BuildKeyAttributes(string partitionKey, string? sortKey)
    {
        // Resolve key attribute names from the model attributes
        var type = typeof(T);
        string? hashName = null;
        string? rangeName = null;

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var hashAttr = prop.GetCustomAttribute<DynamoDBHashKeyAttribute>();
            if (hashAttr != null)
            {
                // Try to get AttributeName property if specified, otherwise fallback to property name
                var nameProp = typeof(DynamoDBHashKeyAttribute).GetProperty("AttributeName");
                hashName = (string?)nameProp?.GetValue(hashAttr) ?? prop.Name;
            }
            var rangeAttr = prop.GetCustomAttribute<DynamoDBRangeKeyAttribute>();
            if (rangeAttr != null)
            {
                var nameProp = typeof(DynamoDBRangeKeyAttribute).GetProperty("AttributeName");
                rangeName = (string?)nameProp?.GetValue(rangeAttr) ?? prop.Name;
            }
        }

        if (string.IsNullOrEmpty(hashName))
            throw new InvalidOperationException($"Type {type.Name} does not define a DynamoDB hash key.");

        var key = new Dictionary<string, AttributeValue>
        {
            [hashName!] = new AttributeValue { S = partitionKey }
        };

        if (!string.IsNullOrEmpty(sortKey) && !string.IsNullOrEmpty(rangeName))
        {
            key[rangeName!] = new AttributeValue { S = sortKey };
        }

        return key;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _context?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
