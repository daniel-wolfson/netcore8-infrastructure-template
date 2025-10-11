using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Custom.Framework.Aws.AuroraDB.Models;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Base DbContext for Aurora database operations
/// Provides connection management and read/write splitting
/// </summary>
public class AuroraDbContext : DbContext
{
    private readonly AuroraDbOptions _options;
    private readonly bool _useReadReplica;

    // DbSets for all entities
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    public AuroraDbContext(DbContextOptions<AuroraDbContext> options, AuroraDbOptions auroraOptions)
        : base(options)
    {
        _options = auroraOptions;
        _useReadReplica = false;
    }

    protected AuroraDbContext(DbContextOptions<AuroraDbContext> options, AuroraDbOptions auroraOptions, bool useReadReplica)
        : base(options)
    {
        _options = auroraOptions;
        _useReadReplica = useReadReplica;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        var connectionString = _options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            ? _options.BuildPostgreSqlConnectionString(_useReadReplica)
            : _options.BuildMySqlConnectionString(_useReadReplica);

        if (_options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                if (_options.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: _options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(_options.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
                npgsqlOptions.CommandTimeout(_options.CommandTimeout);
                npgsqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }
        else if (_options.Engine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
            {
                if (_options.EnableRetryOnFailure)
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: _options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(_options.MaxRetryDelay),
                        errorNumbersToAdd: null);
                }
                mySqlOptions.CommandTimeout(_options.CommandTimeout);
                mySqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }

        if (_options.EnableSensitiveDataLogging)
            optionsBuilder.EnableSensitiveDataLogging();

        if (_options.EnableDetailedErrors)
            optionsBuilder.EnableDetailedErrors();

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Customer entity
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(e => e.Orders)
                .WithOne(e => e.Customer)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(e => e.OrderItems)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Sku);
        });

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Category, e.IsActive });

            entity.HasMany(e => e.OrderItems)
                .WithOne(e => e.Product)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Auto-discover entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Create a read-only context for querying read replicas
    /// </summary>
    public AuroraDbContext CreateReadReplicaContext()
    {
        if (!_options.EnableReadReplicas || string.IsNullOrEmpty(_options.ReadEndpoint))
        {
            return this; // Return the same context if read replicas are not configured
        }

        var options = new DbContextOptionsBuilder<AuroraDbContext>()
            .Options;

        return new AuroraDbContext(options, _options, useReadReplica: true);
    }
}
