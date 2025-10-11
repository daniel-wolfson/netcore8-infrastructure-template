namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Configuration options for AWS Aurora database connection
/// </summary>
public class AuroraDbOptions
{
    /// <summary>
    /// Database engine type: PostgreSQL or MySQL
    /// </summary>
    public string Engine { get; set; } = "PostgreSQL";

    /// <summary>
    /// Aurora cluster write endpoint (primary instance)
    /// </summary>
    public string WriteEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Aurora cluster read endpoint (load balanced across read replicas)
    /// </summary>
    public string? ReadEndpoint { get; set; }

    /// <summary>
    /// Database name
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Database username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Database password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Database port (default: 5432 for PostgreSQL, 3306 for MySQL)
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Maximum number of connections in the pool
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Minimum number of connections to maintain in the pool
    /// </summary>
    public int MinPoolSize { get; set; } = 10;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 60;

    /// <summary>
    /// Enable automatic retry on transient failures
    /// </summary>
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Maximum delay between retries in seconds
    /// </summary>
    public int MaxRetryDelay { get; set; } = 30;

    /// <summary>
    /// Enable sensitive data logging (use only in development)
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// Enable detailed error messages (use only in development)
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// Enable read replica support for query routing
    /// </summary>
    public bool EnableReadReplicas { get; set; } = true;

    /// <summary>
    /// Use SSL/TLS for database connections
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// SSL mode: Disable, Allow, Prefer, Require, VerifyCA, VerifyFull
    /// </summary>
    public string SSLMode { get; set; } = "Require";

    /// <summary>
    /// Use IAM database authentication
    /// </summary>
    public bool UseIAMAuthentication { get; set; } = false;

    /// <summary>
    /// AWS region for IAM authentication
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Connection string template (optional, overrides individual properties)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Read replica connection string template (optional)
    /// </summary>
    public string? ReadReplicaConnectionString { get; set; }

    /// <summary>
    /// Build PostgreSQL connection string
    /// </summary>
    public string BuildPostgreSqlConnectionString(bool useReadReplica = false)
    {
        if (!string.IsNullOrEmpty(ConnectionString) && !useReadReplica)
            return ConnectionString;

        if (!string.IsNullOrEmpty(ReadReplicaConnectionString) && useReadReplica)
            return ReadReplicaConnectionString;

        var endpoint = useReadReplica && !string.IsNullOrEmpty(ReadEndpoint) 
            ? ReadEndpoint 
            : WriteEndpoint;

        var builder = new System.Text.StringBuilder();
        builder.Append($"Host={endpoint};");
        builder.Append($"Port={Port};");
        builder.Append($"Database={Database};");
        builder.Append($"Username={Username};");
        
        if (!UseIAMAuthentication)
        {
            builder.Append($"Password={Password};");
        }

        builder.Append($"Maximum Pool Size={MaxPoolSize};");
        builder.Append($"Minimum Pool Size={MinPoolSize};");
        builder.Append($"Connection Timeout={ConnectionTimeout};");
        builder.Append($"Command Timeout={CommandTimeout};");
        builder.Append($"Pooling=true;");

        if (UseSSL)
        {
            builder.Append($"SSL Mode={SSLMode};");
            builder.Append("Trust Server Certificate=true;");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Build MySQL connection string
    /// </summary>
    public string BuildMySqlConnectionString(bool useReadReplica = false)
    {
        if (!string.IsNullOrEmpty(ConnectionString) && !useReadReplica)
            return ConnectionString;

        if (!string.IsNullOrEmpty(ReadReplicaConnectionString) && useReadReplica)
            return ReadReplicaConnectionString;

        var endpoint = useReadReplica && !string.IsNullOrEmpty(ReadEndpoint)
            ? ReadEndpoint
            : WriteEndpoint;

        var builder = new System.Text.StringBuilder();
        builder.Append($"Server={endpoint};");
        builder.Append($"Port={Port};");
        builder.Append($"Database={Database};");
        builder.Append($"Uid={Username};");
        
        if (!UseIAMAuthentication)
        {
            builder.Append($"Pwd={Password};");
        }

        builder.Append($"MaximumPoolSize={MaxPoolSize};");
        builder.Append($"MinimumPoolSize={MinPoolSize};");
        builder.Append($"ConnectionTimeout={ConnectionTimeout};");
        builder.Append($"DefaultCommandTimeout={CommandTimeout};");
        builder.Append("Pooling=true;");

        if (UseSSL)
        {
            builder.Append($"SslMode={SSLMode};");
        }

        return builder.ToString();
    }
}
