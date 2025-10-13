# ??? Aurora PostgreSQL with LocalStack - Complete Guide

Complete setup and learning guide for Aurora-compatible PostgreSQL using Docker.

---

## ?? What is Aurora?

**Amazon Aurora** is a relational database service compatible with MySQL and PostgreSQL, offering high performance and availability. We simulate Aurora using PostgreSQL 16 for local development.

### Key Benefits
- ? **High Performance** - Up to 5x faster than standard PostgreSQL
- ? **Scalability** - Read replicas for horizontal scaling
- ? **Reliability** - Automatic failover and backups
- ? **Compatibility** - Drop-in replacement for PostgreSQL

---

## ?? Quick Start (3 Steps)

### Step 1: Start PostgreSQL
```bash
cd Custom.Framework\Aws\LocalStack
on-start.bat
```

### Step 2: Verify Database is Running
```bash
# Health check
docker exec -it aurora-postgres-local pg_isready -U admin -d auroradb

# Test connection
docker exec -it aurora-postgres-local psql -U admin -d auroradb -c "SELECT version();"
```

### Step 3: Query Sample Data
```bash
docker exec -it aurora-postgres-local psql -U admin -d auroradb \
    -c "SELECT * FROM app.customers;"
```

---

## ?? Database Schema

### Schema: `app`

All tables are in the `app` schema for organization.

### Tables

#### 1. **app.customers**
Customer information with balance tracking.

| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| email | VARCHAR(255) | Unique email (indexed) |
| first_name | VARCHAR(100) | First name |
| last_name | VARCHAR(100) | Last name |
| phone | VARCHAR(20) | Phone number |
| balance | DECIMAL(18,2) | Account balance |
| is_active | BOOLEAN | Active status (indexed) |
| created_at | TIMESTAMP | Creation time |
| updated_at | TIMESTAMP | Last update (auto-updated) |

#### 2. **app.products**
Product catalog with inventory.

| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| name | VARCHAR(255) | Product name |
| description | TEXT | Product description |
| sku | VARCHAR(50) | Unique SKU (indexed) |
| price | DECIMAL(18,2) | Unit price |
| quantity | INTEGER | Stock quantity |
| category | VARCHAR(100) | Category (indexed) |
| is_active | BOOLEAN | Active status |
| created_at | TIMESTAMP | Creation time |
| updated_at | TIMESTAMP | Last update |

#### 3. **app.orders**
Order records with customer reference.

| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| customer_id | INTEGER | FK to customers (indexed) |
| order_date | TIMESTAMP | Order date (indexed) |
| total_amount | DECIMAL(18,2) | Total amount |
| status | VARCHAR(50) | Order status (indexed) |
| shipping_address | TEXT | Delivery address |
| payment_method | VARCHAR(50) | Payment type |
| tracking_number | VARCHAR(100) | Shipment tracking |
| created_at | TIMESTAMP | Creation time |
| updated_at | TIMESTAMP | Last update |

#### 4. **app.order_items**
Order line items with product reference.

| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| order_id | INTEGER | FK to orders (indexed) |
| product_id | INTEGER | FK to products (indexed) |
| quantity | INTEGER | Quantity ordered |
| unit_price | DECIMAL(18,2) | Price per unit |
| subtotal | DECIMAL(18,2) | Line total |
| created_at | TIMESTAMP | Creation time |

### Sample Data Included

**Customers (3 records):**
- john.doe@example.com - $1,000.00 balance
- jane.smith@example.com - $500.00 balance
- bob.johnson@example.com - $750.50 balance

**Products (5 records):**
- Laptop Pro - $1,299.99 (50 units)
- Wireless Mouse - $29.99 (200 units)
- USB-C Cable - $14.99 (500 units)
- Monitor 27" - $399.99 (30 units)
- Keyboard Mechanical - $89.99 (100 units)

---

## ?? .NET Integration

### Step 1: Configure
Add to `appsettings.json`:

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
    "MinPoolSize": 10,
    "ConnectionTimeout": 30,
    "CommandTimeout": 60,
    "EnableRetryOnFailure": true,
    "MaxRetryCount": 3
  }
}
```

### Step 2: Register Services
In `Program.cs`:

```csharp
builder.Services.AddAuroraDb(builder.Configuration);
```

### Step 3: Use in Your Code

#### Basic CRUD Operations
```csharp
public class CustomerService
{
    private readonly IAuroraRepository<Customer> _repository;

    public CustomerService(IAuroraRepository<Customer> repository)
    {
        _repository = repository;
    }

    // Create
    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        await _repository.AddAsync(customer);
        await _repository.SaveChangesAsync();
        return customer;
    }

    // Read
    public async Task<Customer?> GetCustomerAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    // Update
    public async Task UpdateBalanceAsync(int id, decimal amount)
    {
        var customer = await _repository.GetByIdAsync(id);
        if (customer != null)
        {
            customer.Balance += amount;
            await _repository.UpdateAsync(customer);
            await _repository.SaveChangesAsync();
        }
    }

    // Delete
    public async Task DeleteCustomerAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
```

#### Complex Queries
```csharp
public class OrderService
{
    private readonly AuroraDbContext _context;

    // Get orders with customer and items
    public async Task<List<Order>> GetOrdersWithDetailsAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.Status == "Pending")
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    // Customer order history
    public async Task<OrderHistory> GetCustomerOrderHistoryAsync(int customerId)
    {
        var orders = await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.OrderItems)
            .ToListAsync();

        return new OrderHistory
        {
            TotalOrders = orders.Count,
            TotalSpent = orders.Sum(o => o.TotalAmount),
            LastOrderDate = orders.Max(o => o.OrderDate)
        };
    }
}
```

#### Transactions
```csharp
public async Task ProcessOrderAsync(OrderRequest request)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Create order
        var order = new Order
        {
            CustomerId = request.CustomerId,
            TotalAmount = request.TotalAmount,
            Status = "Pending",
            OrderDate = DateTime.UtcNow
        };
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Add order items
        foreach (var item in request.Items)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Quantity * item.UnitPrice
            };
            await _context.OrderItems.AddAsync(orderItem);

            // Update product quantity
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.Quantity -= item.Quantity;
            }
        }

        // Update customer balance
        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer != null)
        {
            customer.Balance -= request.TotalAmount;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

## ?? Common SQL Queries

### Basic Queries

#### View All Customers
```sql
SELECT * FROM app.customers ORDER BY last_name;
```

#### Active Products
```sql
SELECT * FROM app.products 
WHERE is_active = true AND quantity > 0
ORDER BY name;
```

#### Recent Orders
```sql
SELECT 
    o.id,
    o.order_date,
    c.first_name || ' ' || c.last_name as customer_name,
    o.total_amount,
    o.status
FROM app.orders o
JOIN app.customers c ON o.customer_id = c.id
ORDER BY o.order_date DESC
LIMIT 10;
```

### Advanced Queries

#### Customer Order Summary
```sql
SELECT 
    c.first_name,
    c.last_name,
    COUNT(o.id) as total_orders,
    SUM(o.total_amount) as total_spent,
    AVG(o.total_amount) as avg_order_value,
    MAX(o.order_date) as last_order_date
FROM app.customers c
LEFT JOIN app.orders o ON c.id = o.customer_id
GROUP BY c.id, c.first_name, c.last_name
ORDER BY total_spent DESC;
```

#### Top Selling Products
```sql
SELECT 
    p.name,
    p.sku,
    SUM(oi.quantity) as total_sold,
    SUM(oi.subtotal) as revenue
FROM app.products p
JOIN app.order_items oi ON p.id = oi.product_id
GROUP BY p.id, p.name, p.sku
ORDER BY total_sold DESC
LIMIT 10;
```

#### Orders with Items
```sql
SELECT 
    o.id as order_id,
    o.order_date,
    c.first_name || ' ' || c.last_name as customer,
    p.name as product_name,
    oi.quantity,
    oi.unit_price,
    oi.subtotal
FROM app.orders o
JOIN app.customers c ON o.customer_id = c.id
JOIN app.order_items oi ON o.id = oi.order_id
JOIN app.products p ON oi.product_id = p.id
WHERE o.status = 'Pending'
ORDER BY o.order_date DESC, o.id, p.name;
```

---

## ?? Performance Optimization

### Indexes

The database includes pre-configured indexes on:
- `customers.email` (unique)
- `customers.is_active`
- `products.sku` (unique)
- `products.category`
- `products.is_active`
- `orders.customer_id`
- `orders.status`
- `orders.order_date`
- `order_items.order_id`
- `order_items.product_id`

### Connection Pooling

```csharp
// Configured automatically
"MaxPoolSize": 100,      // Maximum connections
"MinPoolSize": 10,       // Minimum connections
"ConnectionTimeout": 30  // Timeout in seconds
```

### Batch Operations

```csharp
// Efficient bulk insert
var customers = new List<Customer> { /* 1000 customers */ };
await _repository.BulkInsertAsync(customers);

// Much faster than:
foreach (var customer in customers)
{
    await _repository.AddAsync(customer);
}
await _repository.SaveChangesAsync();
```

### Query Optimization

```csharp
// ? Bad: N+1 query problem
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await _context.Customers.FindAsync(order.CustomerId);
}

// ? Good: Single query with include
var orders = await _context.Orders
    .Include(o => o.Customer)
    .ToListAsync();
```

---

## ? Best Practices

### 1. Use Connection Pooling
```csharp
// ? Automatically configured
// Reuses connections instead of creating new ones
```

### 2. Use Transactions for Multi-Step Operations
```csharp
using var transaction = await _context.BeginTransactionAsync();
try
{
    // Multiple operations
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
}
```

### 3. Use Projections for Large Datasets
```csharp
// ? Bad: Loads all columns
var customers = await _context.Customers.ToListAsync();

// ? Good: Only loads needed columns
var customerNames = await _context.Customers
    .Select(c => new { c.Id, c.FirstName, c.LastName })
    .ToListAsync();
```

### 4. Add Indexes to Frequently Queried Columns
```sql
CREATE INDEX idx_orders_status_date 
ON app.orders(status, order_date DESC);
```

### 5. Use Async Operations
```csharp
// ? Always use async
await _repository.GetAllAsync();

// ? Don't block
_repository.GetAllAsync().Result;
```

---

## ?? Testing

### Run All AuroraDB Tests
```bash
dotnet test --filter "FullyQualifiedName~AuroraDBTests"
```

### Using PowerShell
```powershell
.\sqs-learning.ps1 -TestAurora
```

---

## ?? Troubleshooting

### Can't Connect to Database
```bash
# Check if container is running
docker ps | grep aurora-postgres

# Test connection
docker exec -it aurora-postgres-local pg_isready -U admin -d auroradb

# View logs
docker logs aurora-postgres-local

# Restart container
docker restart aurora-postgres-local
```

### Database Schema Issues
```bash
# Verify tables exist
docker exec -it aurora-postgres-local psql -U admin -d auroradb -c "\dt app.*"

# Check sample data
docker exec -it aurora-postgres-local psql -U admin -d auroradb \
    -c "SELECT COUNT(*) FROM app.customers;"

# Reinitialize database
docker-compose down -v
docker-compose up -d
```

---

## ?? Additional Resources

- **Quick Reference**: [ADB_QUICK_REFERENCE.md](ADB_QUICK_REFERENCE.md)
- **Full Documentation**: `Custom.Framework\Aws\AuroraDB\README.md`
- **AWS Aurora Docs**: https://docs.aws.amazon.com/AmazonRDS/latest/AuroraUserGuide/
- **PostgreSQL Docs**: https://www.postgresql.org/docs/

---

**Ready to start?** Run `on-start.bat` and query your first table!

For SQS guide, see: [SQS_README.md](SQS_README.md)
