# Aurora DB Migrations Guide

## Overview

This guide explains how to initialize the Aurora database and manage schema migrations using Entity Framework Core migrations.

## Prerequisites

- .NET 8 SDK installed
- EF Core CLI tools installed
- PostgreSQL or MySQL database accessible (local or AWS Aurora)

## Install EF Core Tools

```bash
# Install globally (one-time setup)
dotnet tool install --global dotnet-ef

# Or update if already installed
dotnet tool update --global dotnet-ef

# Verify installation
dotnet ef --version
```

## Database Initialization

### Option 1: Using Migrations (Recommended for Production)

This approach creates a versioned history of your database schema changes.

#### 1. Create Initial Migration

```bash
# Navigate to the Custom.Framework project directory
cd Custom.Framework

# Create the initial migration
dotnet ef migrations add InitialCreate --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations

# This creates migration files in: Custom.Framework/Aws/AuroraDB/Migrations/
```

#### 2. Review the Migration

The migration files will be created:
- `YYYYMMDDHHMMSS_InitialCreate.cs` - The migration logic
- `YYYYMMDDHHMMSS_InitialCreate.Designer.cs` - Metadata
- `AuroraDbContextModelSnapshot.cs` - Current model snapshot

#### 3. Apply Migration to Database

```bash
# Update the database
dotnet ef database update --context AuroraDbContext

# Or apply specific migration
dotnet ef database update InitialCreate --context AuroraDbContext
```

### Option 2: Using EnsureCreated (Development/Testing Only)

For quick testing, you can use `EnsureCreated()` in your code:

```csharp
await context.Database.EnsureCreatedAsync();
```

?? **Warning**: `EnsureCreated()` does not use migrations and cannot be mixed with migrations on the same database.

## Configuration

### Local PostgreSQL (Development)

Update `Aurora.appsettings.json`:

```json
{
  "AuroraDB": {
    "Engine": "PostgreSQL",
    "WriteEndpoint": "localhost",
    "Database": "aurora_dev_db",
    "Username": "postgres",
    "Password": "your_password",
    "Port": 5432,
    "UseSSL": false
  }
}
```

### AWS Aurora PostgreSQL (Production)

```json
{
  "AuroraDB": {
    "Engine": "PostgreSQL",
    "WriteEndpoint": "your-cluster.cluster-xxxxx.us-east-1.rds.amazonaws.com",
    "ReadEndpoint": "your-cluster.cluster-ro-xxxxx.us-east-1.rds.amazonaws.com",
    "Database": "production_db",
    "Username": "admin",
    "Password": "secure_password",
    "Port": 5432,
    "UseSSL": true,
    "SSLMode": "Require"
  }
}
```

## Common Migration Commands

### Create a New Migration

After modifying your entity models:

```bash
# Add a new migration
dotnet ef migrations add AddNewFeature --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations

# Examples:
dotnet ef migrations add AddProductDiscountColumn --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations
dotnet ef migrations add AddOrderIndexes --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations
```

### Apply Migrations

```bash
# Update to latest migration
dotnet ef database update --context AuroraDbContext

# Update to specific migration
dotnet ef database update MigrationName --context AuroraDbContext

# Rollback to previous migration
dotnet ef database update PreviousMigrationName --context AuroraDbContext

# Rollback all migrations (WARNING: destroys data)
dotnet ef database update 0 --context AuroraDbContext
```

### View Migrations

```bash
# List all migrations
dotnet ef migrations list --context AuroraDbContext

# Check pending migrations
dotnet ef migrations has-pending-model-changes --context AuroraDbContext
```

### Remove Last Migration

```bash
# Remove the last migration (if not applied to database)
dotnet ef migrations remove --context AuroraDbContext
```

### Generate SQL Script

```bash
# Generate SQL for all migrations
dotnet ef migrations script --context AuroraDbContext --output migration.sql

# Generate SQL for specific range
dotnet ef migrations script FromMigration ToMigration --context AuroraDbContext --output delta.sql

# Generate idempotent script (safe to run multiple times)
dotnet ef migrations script --context AuroraDbContext --idempotent --output migration.sql
```

## Programmatic Migration

Apply migrations from your application code:

```csharp
using Custom.Framework.Aws.AuroraDB;
using Microsoft.EntityFrameworkCore;

// In your startup/initialization code
public async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
    
    // Apply pending migrations
    await context.Database.MigrateAsync();
    
    // Optional: Seed initial data
    await SeedDataAsync(context);
}

private async Task SeedDataAsync(AuroraDbContext context)
{
    // Check if data already exists
    if (await context.Customers.AnyAsync())
        return;
    
    // Add initial data
    var customers = new[]
    {
        new Customer
        {
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            IsActive = true
        }
    };
    
    context.Customers.AddRange(customers);
    await context.SaveChangesAsync();
}
```

## Migration Best Practices

### 1. Always Review Generated Migrations

```bash
# After creating a migration, review the generated code
code Aws/AuroraDB/Migrations/YYYYMMDDHHMMSS_MigrationName.cs
```

### 2. Test Migrations Locally First

```bash
# Test on local database first
dotnet ef database update --context AuroraDbContext

# Verify the changes
# Then apply to staging/production
```

### 3. Backup Before Production Migrations

```bash
# AWS Aurora: Create snapshot before major migrations
aws rds create-db-cluster-snapshot \
    --db-cluster-identifier your-cluster \
    --db-cluster-snapshot-identifier pre-migration-snapshot-$(date +%Y%m%d)
```

### 4. Use Idempotent Scripts for Production

```bash
# Generate idempotent SQL script
dotnet ef migrations script --context AuroraDbContext --idempotent --output migration.sql

# Review and test the script
# Apply manually to production
```

### 5. Handle Data Migrations Carefully

When adding NOT NULL columns to existing tables:

```csharp
public partial class AddRequiredColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add column as nullable
        migrationBuilder.AddColumn<string>(
            name: "new_column",
            table: "customers",
            type: "varchar(100)",
            nullable: true);
        
        // Step 2: Set default values for existing rows
        migrationBuilder.Sql(
            "UPDATE customers SET new_column = 'default_value' WHERE new_column IS NULL");
        
        // Step 3: Make column NOT NULL
        migrationBuilder.AlterColumn<string>(
            name: "new_column",
            table: "customers",
            type: "varchar(100)",
            nullable: false);
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "new_column",
            table: "customers");
    }
}
```

## Troubleshooting

### Error: "Build failed"

```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet ef migrations add MigrationName --context AuroraDbContext
```

### Error: "No DbContext named 'AuroraDbContext' was found"

```bash
# Ensure you're in the correct project directory
cd Custom.Framework

# Specify the project explicitly
dotnet ef migrations add MigrationName --project Custom.Framework.csproj --context AuroraDbContext
```

### Error: "Unable to create migrations configuration"

Ensure `AuroraDbContextFactory.cs` exists and implements `IDesignTimeDbContextFactory<AuroraDbContext>`.

### Error: Connection string issues

```bash
# Set connection string via environment variable
export AuroraDB__WriteEndpoint="localhost"
export AuroraDB__Database="test_db"
export AuroraDB__Username="postgres"
export AuroraDB__Password="password"

# Then run migrations
dotnet ef database update --context AuroraDbContext
```

### Verify Migration Status

```bash
# Check which migrations are applied
dotnet ef migrations list --context AuroraDbContext

# Check pending migrations
dotnet ef migrations has-pending-model-changes --context AuroraDbContext
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Database Migration

on:
  push:
    branches: [ main ]

jobs:
  migrate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef
      
      - name: Generate migration script
        run: |
          cd Custom.Framework
          dotnet ef migrations script --context AuroraDbContext --idempotent --output ../migration.sql
      
      - name: Apply migrations
        run: |
          # Apply using psql or your preferred method
          psql $DATABASE_URL -f migration.sql
        env:
          DATABASE_URL: ${{ secrets.AURORA_CONNECTION_STRING }}
```

## Quick Reference

```bash
# Setup
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add MigrationName --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations

# Apply migrations
dotnet ef database update --context AuroraDbContext

# Generate SQL
dotnet ef migrations script --context AuroraDbContext --idempotent --output migration.sql

# List migrations
dotnet ef migrations list --context AuroraDbContext

# Remove last migration
dotnet ef migrations remove --context AuroraDbContext

# Rollback
dotnet ef database update PreviousMigrationName --context AuroraDbContext
```

## Next Steps

1. Create your first migration: `dotnet ef migrations add InitialCreate`
2. Review the generated migration files
3. Apply to development database: `dotnet ef database update`
4. Test your application
5. Generate production SQL script: `dotnet ef migrations script --idempotent`
6. Apply to staging/production with proper backup and rollback plan

## Additional Resources

- [EF Core Migrations Documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [AWS Aurora Best Practices](https://docs.aws.amazon.com/AmazonRDS/latest/AuroraUserGuide/Aurora.BestPractices.html)
- [Deploying Database Changes](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
