# Aurora DB Quick Reference Card

## Installation (One-time)
```bash
dotnet tool install --global dotnet-ef
```

## Create Migration
```bash
cd Custom.Framework
dotnet ef migrations add MigrationName --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations
```

## Apply Migration
```bash
dotnet ef database update --context AuroraDbContext
```

## List Migrations
```bash
dotnet ef migrations list --context AuroraDbContext
```

## Generate SQL Script
```bash
dotnet ef migrations script --context AuroraDbContext --idempotent --output migration.sql
```

## Remove Last Migration
```bash
dotnet ef migrations remove --context AuroraDbContext
```

## Check Status
```bash
dotnet ef migrations has-pending-model-changes --context AuroraDbContext
```

## Service Registration
```csharp
builder.Services.AddAuroraDb(builder.Configuration);
```

## Initialize Database
```csharp
var initializer = scope.ServiceProvider.GetRequiredService<AuroraDatabaseInitializer>();
await initializer.InitializeAsync(seedData: true);
```

## Use Repository
```csharp
private readonly IAuroraRepository<Customer> _repo;

// Get
var customer = await _repo.GetByIdAsync(id);

// Create
await _repo.AddAsync(customer);
await _repo.SaveChangesAsync();

// Update
await _repo.UpdateAsync(customer);
await _repo.SaveChangesAsync();

// Delete
await _repo.DeleteAsync(customer);
await _repo.SaveChangesAsync();

// Query
var active = await _repo.FindAsync(c => c.IsActive);

// Bulk
await _repo.BulkInsertAsync(customers);
```

## Configuration (Aurora.appsettings.json)
```json
{
  "AuroraDB": {
    "Engine": "PostgreSQL",
    "WriteEndpoint": "localhost",
    "Database": "db_name",
    "Username": "username",
    "Password": "password",
    "Port": 5432
  }
}
```

## Helper Scripts
**Windows**: `.\migrate.ps1`  
**Linux/Mac**: `./migrate.sh`

## Important Files
- **Context**: `AuroraDbContext.cs`
- **Factory**: `AuroraDbContextFactory.cs`
- **Models**: `Models/` folder
- **Migrations**: `Migrations/` folder (auto-created)
- **Config**: `Aurora.appsettings.json`

## Common Workflows

### Add New Entity
1. Create entity class in `Models/`
2. Add `DbSet<Entity>` to `AuroraDbContext`
3. Configure in `OnModelCreating()` (if needed)
4. Create migration: `dotnet ef migrations add AddEntity`
5. Apply: `dotnet ef database update`

### Modify Existing Entity
1. Update entity class
2. Create migration: `dotnet ef migrations add UpdateEntity`
3. Review generated migration
4. Apply: `dotnet ef database update`

### Production Deployment
1. Generate SQL: `dotnet ef migrations script --idempotent -o migration.sql`
2. Review SQL script
3. Backup database
4. Apply SQL to production
5. Verify application

## Documentation
- **Setup Guide**: `SETUP_COMPLETE.md`
- **Migrations**: `MIGRATIONS.md`  
- **Usage**: `README.md`
