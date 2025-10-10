namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Configuration options for AWS DynamoDB
/// </summary>
public class DynamoDbOptions
{
    /// <summary>
    /// AWS region (e.g., us-east-1, eu-west-1)
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// AWS Access Key ID (optional if using IAM roles)
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS Secret Access Key (optional if using IAM roles)
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Service URL (optional, for local DynamoDB)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Default table name
    /// </summary>
    public string TableName { get; set; } = "DefaultTable";

    /// <summary>
    /// Enable batch processing for write operations
    /// </summary>
    public bool EnableBatchProcessing { get; set; } = true;

    /// <summary>
    /// Maximum batch size for write operations (DynamoDB limit is 25)
    /// </summary>
    public int MaxBatchSize { get; set; } = 25;

    /// <summary>
    /// Maximum retries for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for operations in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable request metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Read capacity units for provisioned tables
    /// </summary>
    public long ReadCapacityUnits { get; set; } = 5;

    /// <summary>
    /// Write capacity units for provisioned tables
    /// </summary>
    public long WriteCapacityUnits { get; set; } = 5;

    /// <summary>
    /// Use on-demand billing mode instead of provisioned
    /// </summary>
    public bool UseOnDemandBilling { get; set; } = true;
}
