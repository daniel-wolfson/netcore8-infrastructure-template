using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Aws.AmazonSQS;

/// <summary>
/// Extension methods for configuring Amazon SQS services
/// </summary>
public static class AmazonSqsExtensions
{
    /// <summary>
    /// Add Amazon SQS services to the service collection
    /// </summary>
    public static IServiceCollection AddAmazonSqs(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<AmazonSqsOptions>(configuration.GetSection("AmazonSQS"));

        // Register SQS client
        services.AddSingleton<IAmazonSQS>(serviceProvider =>
        {
            var options = configuration.GetSection("AmazonSQS").Get<AmazonSqsOptions>()
                ?? throw new InvalidOperationException("AmazonSQS configuration is missing");

            var config = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                MaxErrorRetry = options.MaxRetries
            };

            // Use local SQS if ServiceUrl is configured (e.g., LocalStack)
            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            // Create client with credentials if provided, otherwise use IAM role
            if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            {
                var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
                return new AmazonSQSClient(credentials, config);
            }

            return new AmazonSQSClient(config);
        });

        // Register SQS client wrapper
        services.AddScoped<ISqsClient, SqsClient>();

        return services;
    }

    /// <summary>
    /// Add Amazon SQS services with custom configuration action
    /// </summary>
    public static IServiceCollection AddAmazonSqs(
        this IServiceCollection services,
        Action<AmazonSqsOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IAmazonSQS>(serviceProvider =>
        {
            var options = new AmazonSqsOptions();
            configureOptions(options);

            var config = new AmazonSQSConfig
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
                return new AmazonSQSClient(credentials, config);
            }

            return new AmazonSQSClient(config);
        });

        services.AddScoped<ISqsClient, SqsClient>();

        return services;
    }
}
