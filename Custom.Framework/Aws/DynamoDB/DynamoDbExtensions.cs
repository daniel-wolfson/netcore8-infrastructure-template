using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Extension methods for configuring DynamoDB services
/// </summary>
public static class DynamoDbExtensions
{
    /// <summary>
    /// Add DynamoDB services to the service collection
    /// </summary>
    public static IServiceCollection AddDynamoDb(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<DynamoDbOptions>(configuration.GetSection("DynamoDB"));

        // Register DynamoDB client
        services.AddSingleton<IAmazonDynamoDB>(serviceProvider =>
        {
            var options = configuration.GetSection("DynamoDB").Get<DynamoDbOptions>()
                ?? throw new InvalidOperationException("DynamoDB configuration is missing");

            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                MaxErrorRetry = options.MaxRetries
            };

            // Use local DynamoDB if ServiceUrl is configured
            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            // Create client with credentials if provided, otherwise use IAM role
            if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            {
                var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
                return new AmazonDynamoDBClient(credentials, config);
            }

            return new AmazonDynamoDBClient(config);
        });

        // Register repository
        services.AddScoped(typeof(IDynamoDbRepository<>), typeof(DynamoDbRepository<>));

        return services;
    }

    /// <summary>
    /// Add DynamoDB services with custom configuration action
    /// </summary>
    public static IServiceCollection AddDynamoDb(
        this IServiceCollection services,
        Action<DynamoDbOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IAmazonDynamoDB>(serviceProvider =>
        {
            var options = new DynamoDbOptions();
            configureOptions(options);

            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                MaxErrorRetry = options.MaxRetries
            };

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            {
                var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
                return new AmazonDynamoDBClient(credentials, config);
            }

            return new AmazonDynamoDBClient(config);
        });

        services.AddScoped(typeof(IDynamoDbRepository<>), typeof(DynamoDbRepository<>));

        return services;
    }
}
