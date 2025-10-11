using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Extension methods for configuring Aurora database services
/// </summary>
public static class AuroraDbExtensions
{
    /// <summary>
    /// Add Aurora database services to the service collection
    /// </summary>
    public static IServiceCollection AddAuroraDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        var options = configuration.GetSection("AuroraDB").Get<AuroraDbOptions>()
            ?? throw new InvalidOperationException("AuroraDB configuration is missing");

        services.AddSingleton(options);
        services.Configure<AuroraDbOptions>(configuration.GetSection("AuroraDB"));

        // Register DbContext
        services.AddDbContext<AuroraDbContext>((serviceProvider, optionsBuilder) =>
        {
            ConfigureDbContext(optionsBuilder, options);
        });

        // Register repository
        services.AddScoped(typeof(IAuroraRepository<>), typeof(AuroraRepository<>));

        // Register database initializer
        services.AddScoped<AuroraDatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Add Aurora database services with custom configuration action
    /// </summary>
    public static IServiceCollection AddAuroraDb(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuroraDbOptions> configureOptions)
    {
        var options = configuration.GetSection("AuroraDB").Get<AuroraDbOptions>()
            ?? new AuroraDbOptions();

        configureOptions(options);

        services.AddSingleton(options);
        services.Configure(configureOptions);

        services.AddDbContext<AuroraDbContext>((serviceProvider, optionsBuilder) =>
        {
            ConfigureDbContext(optionsBuilder, options);
        });

        services.AddScoped(typeof(IAuroraRepository<>), typeof(AuroraRepository<>));
        services.AddScoped<AuroraDatabaseInitializer>();

        return services;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, AuroraDbOptions options)
    {
        var connectionString = options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            ? options.BuildPostgreSqlConnectionString()
            : options.BuildMySqlConnectionString();

        if (options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                if (options.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
                npgsqlOptions.CommandTimeout(options.CommandTimeout);
                npgsqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }
        else if (options.Engine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
            {
                if (options.EnableRetryOnFailure)
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelay),
                        errorNumbersToAdd: null);
                }
                mySqlOptions.CommandTimeout(options.CommandTimeout);
                mySqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }

        if (options.EnableSensitiveDataLogging)
            optionsBuilder.EnableSensitiveDataLogging();

        if (options.EnableDetailedErrors)
            optionsBuilder.EnableDetailedErrors();
    }

    /// <summary>
    /// Extension to use read replica for a query
    /// </summary>
    public static IQueryable<T> UseReadReplica<T>(this IQueryable<T> query) where T : class
    {
        return query.AsNoTracking();
    }
}
