# ?? Aurora PostgreSQL - Quick Reference Card

Complete command reference for Aurora PostgreSQL with LocalStack.

---

## ?? Getting Started

### Start AuroraDB Service
```bash
# Windows
on-start.bat

# PowerShell
.\sqs-learning.ps1 -Start

# Linux/Mac
docker-compose up -d
```

### Check AuroraDB Status
```bash
# PowerShell
.\sqs-learning.ps1 -Status

# Health check
docker exec -it aurora-postgres-local pg_isready -U admin -d auroradb

# Test connection
docker exec -it aurora-postgres-local psql -U admin -d auroradb -c "SELECT 1;"
```

---

## ??? Docker Commands

#### Connect to Database
```bash
docker exec -it aurora-postgres-local psql -U admin -d auroradb
```

#### Run Query from CLI
```bash
docker exec -it aurora-postgres-local psql -U admin -d auroradb \
    -c "SELECT * FROM app.customers;"
```

#### Export Data
```bash
docker exec -it aurora-postgres-local pg_dump -U admin auroradb > backup.sql
```

#### Import Data
```bash
docker exec -i aurora-postgres-local psql -U admin -d auroradb < backup.sql
```

#### View Logs
```bash
docker logs aurora-postgres-local -f
```

---

## ?? SQL Commands (psql)

### Database Navigation

#### List Tables
```sql
\dt app.*
```

#### Describe Table
```sql
\d+ app.customers
```

#### List Schemas
```sql
\dn
```

#### Show Current Database
```sql
SELECT current_database();
```

### Basic Queries

#### Select Data
```sql
SELECT * FROM app.customers LIMIT 10;
```

#### Insert Customer
```sql
INSERT INTO app.customers (email, first_name, last_name, balance)
VALUES ('test@example.com', 'Test', 'User', 100.00);
```

#### Update Customer
```sql
UPDATE app.customers 
SET balance = balance + 50 
WHERE email = 'test@example.com';
```

#### Delete Customer
```sql
DELETE FROM app.customers WHERE email = 'test@example.com';
```

### Advanced Queries

#### Join Query
```sql
SELECT c.first_name, c.last_name, o.order_date, o.total_amount
FROM app.customers c
JOIN app.orders o ON c.id = o.customer_id
ORDER BY o.order_date DESC
LIMIT 10;
```

#### Aggregate Query
```sql
SELECT 
    c.first_name, 
    c.last_name,
    COUNT(o.id) as order_count,
    SUM(o.total_amount) as total_spent
FROM app.customers c
LEFT JOIN app.orders o ON c.id = o.customer_id
GROUP BY c.id, c.first_name, c.last_name
ORDER BY total_spent DESC;
```

#### Subquery
```sql
SELECT * FROM app.customers
WHERE id IN (
    SELECT DISTINCT customer_id FROM app.orders
    WHERE total_amount > 500
);
```

---

## ?? .NET API (EF Core)

### Basic Operations

#### Get by ID
```csharp
var customer = await _repository.GetByIdAsync(customerId);
```

#### Get All
```csharp
var customers = await _repository.GetAllAsync();
```

#### Query with Filter
```csharp
var activeCustomers = await _repository
    .GetQueryable()
    .Where(c => c.IsActive)
    .OrderBy(c => c.LastName)
    .ToListAsync();
```

#### Add Entity
```csharp
await _repository.AddAsync(customer);
await _repository.SaveChangesAsync();
```

#### Update Entity
```csharp
customer.Balance += 100;
await _repository.UpdateAsync(customer);
await _repository.SaveChangesAsync();
```

#### Delete Entity
```csharp
await _repository.DeleteAsync(customerId);
await _repository.SaveChangesAsync();
```

### Advanced Operations

#### Bulk Insert
```csharp
var customers = new List<Customer> { /* ... */ };
await _repository.BulkInsertAsync(customers);
```

#### Transaction
```csharp
using var transaction = await _context.BeginTransactionAsync();
try
{
    await _repository.AddAsync(order);
    await _repository.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

#### Raw SQL Query
```csharp
var customers = await _context.Customers
    .FromSqlRaw("SELECT * FROM app.customers WHERE balance > {0}", 1000)
    .ToListAsync();
```

#### Complex Query
```csharp
var result = await _context.Orders
    .Include(o => o.Customer)
    .Include(o => o.OrderItems)
        .ThenInclude(oi => oi.Product)
    .Where(o => o.Status == "Pending")
    .OrderByDescending(o => o.OrderDate)
    .ToListAsync();
```

---

## ??? Database Tables

| Table | Description | Key Columns |
|-------|-------------|-------------|
| app.customers | Customer data | id, email, first_name, last_name, balance |
| app.products | Product catalog | id, sku, name, price, quantity |
| app.orders | Order records | id, customer_id, total_amount, status |
| app.order_items | Order line items | id, order_id, product_id, quantity |

### Sample Data

**Customers (3 records):**
- john.doe@example.com - Balance: $1,000.00
- jane.smith@example.com - Balance: $500.00
- bob.johnson@example.com - Balance: $750.50

**Products (5 records):**
- Laptop Pro - $1,299.99 (50 in stock)
- Wireless Mouse - $29.99 (200 in stock)
- USB-C Cable - $14.99 (500 in stock)
- Monitor 27" - $399.99 (30 in stock)
- Keyboard Mechanical - $89.99 (100 in stock)

---

## ?? Connection Details

```
Host: localhost
Port: 5432
Database: auroradb
Username: admin
Password: localpassword
Schema: app
```

**.NET Connection String:**
```
Host=localhost;Port=5432;Database=auroradb;Username=admin;Password=localpassword;Maximum Pool Size=100;
```

**.NET Configuration (appsettings.json):**
```json
{
  "AuroraDB": {
    "Engine": "PostgreSQL",
    "WriteEndpoint": "localhost",
    "Database": "auroradb",
    "Username": "admin",
    "Password": "localpassword",
    "Port": 5432,
    "MaxPoolSize": 100,
    "EnableRetryOnFailure": true
  }
}
```

---

## ?? Testing

```bash
# All AuroraDB tests
dotnet test --filter "FullyQualifiedName~AuroraDBTests"

# PowerShell
.\sqs-learning.ps1 -TestAurora
```

---

## ?? Common Patterns

### CRUD Operations
```csharp
// Create
var customer = new Customer { Email = "test@example.com" };
await _repository.AddAsync(customer);
await _repository.SaveChangesAsync();

// Read
var customer = await _repository.GetByIdAsync(id);

// Update
customer.Balance += 100;
await _repository.UpdateAsync(customer);
await _repository.SaveChangesAsync();

// Delete
await _repository.DeleteAsync(id);
await _repository.SaveChangesAsync();
```

### Order Processing
```csharp
using var transaction = await _context.BeginTransactionAsync();
try
{
    // Create order
    var order = new Order
    {
        CustomerId = customerId,
        TotalAmount = totalAmount,
        Status = "Pending"
    };
    await _orderRepository.AddAsync(order);
    
    // Add order items
    foreach (var item in orderItems)
    {
        await _orderItemRepository.AddAsync(item);
    }
    
    // Update customer balance
    customer.Balance -= totalAmount;
    await _customerRepository.UpdateAsync(customer);
    
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## ?? Troubleshooting

### Can't Connect
```bash
# Test connection
docker exec -it aurora-postgres-local pg_isready -U admin -d auroradb

# Check logs
docker logs aurora-postgres-local

# Verify port
netstat -an | findstr "5432"

# Restart container
docker restart aurora-postgres-local
```

### Database Issues
```bash
# Reset database
docker-compose down -v
docker-compose up -d

# Check tables exist
docker exec -it aurora-postgres-local psql -U admin -d auroradb -c "\dt app.*"

# Verify sample data
docker exec -it aurora-postgres-local psql -U admin -d auroradb \
    -c "SELECT COUNT(*) FROM app.customers;"
```

---

## ?? Performance Tips

- ? Connection pooling (MaxPoolSize: 100)
- ? Batch inserts (100+ rows at once)
- ? Use indexes on frequently queried columns
- ? Prepared statements (EF Core handles this)
- ? Avoid N+1 queries (use `.Include()`)
- ? Use projections for large datasets

---

## ?? Useful SQL Queries

### Database Statistics
```sql
-- Table sizes
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'app'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;

-- Row counts
SELECT 
    schemaname,
    tablename,
    n_live_tup as row_count
FROM pg_stat_user_tables
WHERE schemaname = 'app'
ORDER BY n_live_tup DESC;
```

### Index Information
```sql
-- List indexes
SELECT
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'app'
ORDER BY tablename, indexname;
```

### Active Connections
```sql
SELECT 
    datname as database,
    count(*) as connections
FROM pg_stat_activity
GROUP BY datname;
```

---

**For detailed guide, see:** `Custom.Framework\Aws\AuroraDB\README.md`
**For SQS commands, see:** `SQS_QUICK_REFERENCE.md`
