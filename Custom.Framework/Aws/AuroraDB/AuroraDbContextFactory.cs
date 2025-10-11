using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// This is used by EF Core tools (dotnet ef) to create the database context
/// </summary>
public class AuroraDbContextFactory : IDesignTimeDbContextFactory<AuroraDbContext>
{
    public AuroraDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Aurora.appsettings.json", optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get Aurora options from configuration
        var auroraOptions = configuration.GetSection("AuroraDB").Get<AuroraDbOptions>()
            ?? new AuroraDbOptions
            {
                Engine = "PostgreSQL",
                WriteEndpoint = "localhost",
                Database = "aurora_db",
                Username = "postgres",
                Password = "postgres",
                Port = 5432,
                UseSSL = false
            };

        // Build connection string
        var connectionString = auroraOptions.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            ? auroraOptions.BuildPostgreSqlConnectionString()
            : auroraOptions.BuildMySqlConnectionString();

        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<AuroraDbContext>();

        if (auroraOptions.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("Custom.Framework");
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });
        }
        else if (auroraOptions.Engine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
            {
                mySqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }

        return new AuroraDbContext(optionsBuilder.Options, auroraOptions);
    }
}
