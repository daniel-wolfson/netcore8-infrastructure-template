using Custom.Framework.Azure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// DbContext for Azure Cosmos DB using EF Core Cosmos provider
/// Manages order context data for hospitality reservation flow
/// </summary>
public class OrderDbContext : DbContext
{
    private readonly CosmosDbOptions _options;

    public OrderDbContext(DbContextOptions<OrderDbContext> options, CosmosDbOptions cosmosOptions)
        : base(options)
    {
        _options = cosmosOptions;
    }

    /// <summary>
    /// Order contexts collection
    /// </summary>
    public DbSet<OrderContext> Orders { get; set; } = null!;

    public override int SaveChanges()
    {
        SetOrderDefaults();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetOrderDefaults();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void SetOrderDefaults()
    {
        var entries = ChangeTracker.Entries<OrderContext>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                // Set timestamps
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;

                // Set TTL if not already set
                if (!entry.Entity.Ttl.HasValue)
                {
                    SetTtlByStatus(entry.Entity);
                }

                // Calculate expiration time
                if (entry.Entity.Ttl.HasValue && entry.Entity.Ttl.Value > 0)
                {
                    entry.Entity.ExpiresAt = DateTime.UtcNow.AddSeconds(entry.Entity.Ttl.Value);
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Update timestamp
                entry.Entity.UpdatedAt = DateTime.UtcNow;

                // Update TTL if status changed
                if (entry.Property(e => e.Status).IsModified)
                {
                    SetTtlByStatus(entry.Entity);
                    
                    // Recalculate expiration time
                    if (entry.Entity.Ttl.HasValue && entry.Entity.Ttl.Value > 0)
                    {
                        entry.Entity.ExpiresAt = DateTime.UtcNow.AddSeconds(entry.Entity.Ttl.Value);
                    }
                }
            }
        }
    }

    private void SetTtlByStatus(OrderContext order)
    {
        order.Ttl = order.Status switch
        {
            OrderStatus.Succeeded => _options.SucceededTtlSeconds,
            OrderStatus.Failed => _options.DefaultTtlSeconds,
            OrderStatus.Cancelled => _options.DefaultTtlSeconds,
            OrderStatus.Expired => _options.DefaultTtlSeconds,
            _ => _options.DefaultTtlSeconds
        };
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        var endpoint = _options.GetEndpoint();
        var key = _options.GetKey();

        optionsBuilder.UseCosmos(
            accountEndpoint: endpoint,
            accountKey: key,
            databaseName: _options.DatabaseName,
            cosmosOptionsAction: cosmosOptions =>
            {
                // Configure SSL validation for emulator first
                if (_options.UseEmulator)
                {
                    cosmosOptions.HttpClientFactory(() =>
                    {
                        HttpMessageHandler httpMessageHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
                        };
                        return new HttpClient(httpMessageHandler);
                    });
                    
                    // Use Gateway mode for emulator (required for SSL bypass)
                    cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
                }
                else
                {
                    // Production: Use configured connection mode
                    cosmosOptions.ConnectionMode(_options.ConnectionMode == "Direct" 
                        ? Microsoft.Azure.Cosmos.ConnectionMode.Direct 
                        : Microsoft.Azure.Cosmos.ConnectionMode.Gateway);

                    // These settings only apply to Direct mode
                    if (_options.ConnectionMode == "Direct")
                    {
                        cosmosOptions.MaxRequestsPerTcpConnection(_options.MaxConcurrentConnections ?? 16);
                        cosmosOptions.MaxTcpConnectionsPerEndpoint(_options.MaxConcurrentConnections ?? 16);
                    }
                }

                cosmosOptions.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeout));
                
                if (_options.ApplicationRegion != null)
                {
                    cosmosOptions.Region(_options.ApplicationRegion);
                }
            });

        if (_options.EnableDetailedLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultContainer(_options.ContainerName);

        // Configure OrderContext entity
        modelBuilder.Entity<OrderContext>(entity =>
        {
            // Set container name
            entity.ToContainer(_options.ContainerName);

            // Configure partition key
            entity.HasPartitionKey(o => o.HotelCode);

            // Configure primary key
            entity.HasKey(o => o.Id);

            // Configure properties
            entity.Property(o => o.Id)
                .ToJsonProperty("id")
                .IsRequired();

            entity.Property(o => o.HotelCode)
                .ToJsonProperty("hotelCode")
                .IsRequired();

            entity.Property(o => o.SessionId)
                .ToJsonProperty("sessionId")
                .IsRequired();

            entity.Property(o => o.CustomerId)
                .ToJsonProperty("customerId");

            entity.Property(o => o.Status)
                .ToJsonProperty("status")
                .IsRequired();

            entity.Property(o => o.CurrentStep)
                .ToJsonProperty("currentStep")
                .IsRequired();

            entity.Property(o => o.CreatedAt)
                .ToJsonProperty("createdAt")
                .IsRequired();

            entity.Property(o => o.UpdatedAt)
                .ToJsonProperty("updatedAt")
                .IsRequired();

            entity.Property(o => o.ExpiresAt)
                .ToJsonProperty("expiresAt");

            entity.Property(o => o.Ttl)
                .ToJsonProperty("ttl");

            entity.Property(o => o.ETag)
                .ToJsonProperty("_etag")
                .IsETagConcurrency();

            // Configure owned types
            entity.OwnsOne(o => o.OrderData, od =>
            {
                od.ToJsonProperty("orderData");
                od.OwnsOne(d => d.GuestInfo, gi => gi.ToJsonProperty("guestInfo"));
            });

            entity.OwnsOne(o => o.PaymentInfo, pi => pi.ToJsonProperty("paymentInfo"));
            entity.OwnsOne(o => o.VerificationResult, vr => vr.ToJsonProperty("verificationResult"));

            // Configure indexes for efficient querying
            entity.HasIndex(o => o.SessionId);
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.CreatedAt);
            entity.HasIndex(o => new { o.HotelCode, o.Status });
            entity.HasIndex(o => new { o.HotelCode, o.SessionId });
        });

        base.OnModelCreating(modelBuilder);
    }
}
