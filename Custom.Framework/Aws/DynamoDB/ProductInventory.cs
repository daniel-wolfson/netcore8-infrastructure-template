using Amazon.DynamoDBv2.DataModel;

namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Scenario: Product inventory for e-commerce platform
/// Use case: High-frequency read/write for stock management
/// </summary>
[DynamoDBTable("ProductInventory")]
public class ProductInventory
{
    /// <summary>
    /// Partition Key: Product SKU
    /// </summary>
    [DynamoDBHashKey("ProductSku")]
    public string ProductSku { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: Warehouse location
    /// </summary>
    [DynamoDBRangeKey("WarehouseId")]
    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>
    /// Available quantity
    /// </summary>
    [DynamoDBProperty("Quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Reserved quantity (in pending orders)
    /// </summary>
    [DynamoDBProperty("ReservedQuantity")]
    public int ReservedQuantity { get; set; }

    /// <summary>
    /// Product name
    /// </summary>
    [DynamoDBProperty("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Product category
    /// </summary>
    [DynamoDBProperty("Category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Current price
    /// </summary>
    [DynamoDBProperty("Price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Warehouse location details
    /// </summary>
    [DynamoDBProperty("WarehouseName")]
    public string WarehouseName { get; set; } = string.Empty;

    /// <summary>
    /// Warehouse region
    /// </summary>
    [DynamoDBProperty("Region")]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Last restocked date
    /// </summary>
    [DynamoDBProperty("LastRestockedAt")]
    public DateTime? LastRestockedAt { get; set; }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [DynamoDBProperty("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reorder threshold
    /// </summary>
    [DynamoDBProperty("ReorderThreshold")]
    public int ReorderThreshold { get; set; }

    /// <summary>
    /// Is product active
    /// </summary>
    [DynamoDBProperty("IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Version for optimistic locking
    /// </summary>
    [DynamoDBVersion]
    public int? Version { get; set; }
}
