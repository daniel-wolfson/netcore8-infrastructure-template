# GraphQL Implementation Guide for .NET Core 8

This guide provides step-by-step instructions for implementing GraphQL in a .NET Core 8 application.

## Overview

GraphQL is a query language and runtime for APIs that allows clients 
to request exactly the data they need. This implementation guide covers Hot Chocolate, the most popular GraphQL server for .NET.

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code
- Basic understanding of C# and ASP.NET Core
- Understanding of dependency injection

---

## Implementation Steps

### GraphQL-Step-1: Install Required Packages

**TODO: Add GraphQL NuGet packages to your project**

```bash
# Core GraphQL server
dotnet add package HotChocolate.AspNetCore

# Entity Framework integration (if using EF Core)
dotnet add package HotChocolate.Data.EntityFramework

# Filtering, sorting, and projection
dotnet add package HotChocolate.Data

# Authorization support
dotnet add package HotChocolate.AspNetCore.Authorization

# Subscriptions (real-time features)
dotnet add package HotChocolate.Subscriptions.InMemory

# Optional: GraphQL Playground UI
dotnet add package HotChocolate.AspNetCore.Playground 
(obsolete, in .Net8 the HotChocolate.AspNetCore already contains inner mechanism named "Banana Cake Pop")
```

### GraphQL-Step-2: Create Domain Models

**TODO: Define your domain entities**

```csharp
// Models/Product.cs
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Models/Category.cs
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
```

### GraphQL-Step-3: Setup DbContext (Optional - if using Entity Framework)

**TODO: Configure Entity Framework DbContext**

```csharp
// Data/ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });
    }
}
```

### GraphQL-Step-4: Create GraphQL Types

**TODO: Define GraphQL object types**

```csharp
// GraphQL/Types/ProductType.cs
public class ProductType : ObjectType<Product>
{
    protected override void Configure(IObjectTypeDescriptor<Product> descriptor)
    {
        descriptor.Description("Represents a product in the catalog");
        
        descriptor.Field(p => p.Id)
            .Description("The unique identifier of the product");
            
        descriptor.Field(p => p.Name)
            .Description("The name of the product");
            
        descriptor.Field(p => p.Description)
            .Description("The description of the product");
            
        descriptor.Field(p => p.Price)
            .Description("The price of the product");
            
        descriptor.Field(p => p.Category)
            .Description("The category this product belongs to");
    }
}

// GraphQL/Types/CategoryType.cs
public class CategoryType : ObjectType<Category>
{
    protected override void Configure(IObjectTypeDescriptor<Category> descriptor)
    {
        descriptor.Description("Represents a product category");
        
        descriptor.Field(c => c.Products)
            .Description("Products in this category")
            .UseFiltering()
            .UseSorting();
    }
}
```

### GraphQL-Step-5: Create Input Types

**TODO: Define input types for mutations**

```csharp
// GraphQL/Inputs/ProductInput.cs
public class AddProductInput
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
}

public class UpdateProductInput
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int? CategoryId { get; set; }
}

// GraphQL/Inputs/CategoryInput.cs
public class AddCategoryInput
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

### GraphQL-Step-6: Create Services/Repositories

**TODO: Implement data access layer**

```csharp
// Services/IProductService.cs
public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product> AddProductAsync(AddProductInput input);
    Task<Product?> UpdateProductAsync(UpdateProductInput input);
    Task<bool> DeleteProductAsync(int id);
}

// Services/ProductService.cs
public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> AddProductAsync(AddProductInput input)
    {
        var product = new Product
        {
            Name = input.Name,
            Description = input.Description,
            Price = input.Price,
            CategoryId = input.CategoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        return await GetProductByIdAsync(product.Id) ?? product;
    }

    // Implement other methods...
}
```

### GraphQL-Step-7: Create Query Resolvers

**TODO: Define GraphQL queries**

```csharp
// GraphQL/Queries/ProductQueries.cs
[ExtendObjectType(typeof(Query))]
public class ProductQueries
{
    [UseDbContext(typeof(ApplicationDbContext))]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts([ScopedService] ApplicationDbContext context)
    {
        return context.Products.Include(p => p.Category);
    }

    [UseDbContext(typeof(ApplicationDbContext))]
    public async Task<Product?> GetProductById(
        int id,
        [ScopedService] ApplicationDbContext context)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}

// GraphQL/Queries/CategoryQueries.cs
[ExtendObjectType(typeof(Query))]
public class CategoryQueries
{
    [UseDbContext(typeof(ApplicationDbContext))]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Category> GetCategories([ScopedService] ApplicationDbContext context)
    {
        return context.Categories.Include(c => c.Products);
    }
}

// GraphQL/Query.cs
public class Query
{
    public string Hello() => "Hello, GraphQL!";
}
```

### GraphQL-Step-8: Create Mutation Resolvers

**TODO: Define GraphQL mutations**

```csharp
// GraphQL/Mutations/ProductMutations.cs
[ExtendObjectType(typeof(Mutation))]
public class ProductMutations
{
    public async Task<Product> AddProduct(
        AddProductInput input,
        [Service] IProductService productService)
    {
        return await productService.AddProductAsync(input);
    }

    public async Task<Product?> UpdateProduct(
        UpdateProductInput input,
        [Service] IProductService productService)
    {
        return await productService.UpdateProductAsync(input);
    }

    public async Task<bool> DeleteProduct(
        int id,
        [Service] IProductService productService)
    {
        return await productService.DeleteProductAsync(id);
    }
}

// GraphQL/Mutation.cs
public class Mutation
{
    // Base mutation class - extended by specific mutation classes
}
```

### GraphQL-Step-9: Create Subscriptions (Optional)

**TODO: Implement real-time subscriptions**

```csharp
// GraphQL/Subscriptions/ProductSubscriptions.cs
[ExtendObjectType(typeof(Subscription))]
public class ProductSubscriptions
{
    [Subscribe]
    public Product OnProductAdded([EventMessage] Product product) => product;

    [Subscribe]
    public Product OnProductUpdated([EventMessage] Product product) => product;
}

// GraphQL/Subscription.cs
public class Subscription
{
    // Base subscription class
}
```

### GraphQL-Step-10: Configure Services in Program.cs

**TODO: Register GraphQL services and configure the pipeline**

```csharp
// Program.cs
using Microsoft.EntityFrameworkCore;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Playground;

var builder = WebApplication.CreateBuilder(args);

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add application services
builder.Services.AddScoped<IProductService, ProductService>();

// Add GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddTypeExtension<ProductQueries>()
    .AddTypeExtension<ProductMutations>()
    .AddTypeExtension<ProductSubscriptions>()
    .AddType<ProductType>()
    .AddType<CategoryType>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddInMemorySubscriptions()
    .AddAuthorization();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UsePlayground(); // GraphQL Playground UI
}

app.UseRouting();
app.UseAuthentication(); // If using authentication
app.UseAuthorization();

app.MapGraphQL();

app.Run();
```

### GraphQL-Step-11: Add Error Handling

**TODO: Implement proper error handling**

```csharp
// GraphQL/ErrorFilters/GlobalErrorFilter.cs
public class GlobalErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        return error.Exception switch
        {
            ArgumentException => error.WithCode("INVALID_ARGUMENT"),
            UnauthorizedAccessException => error.WithCode("UNAUTHORIZED"),
            _ => error
        };
    }
}

// Register in Program.cs
builder.Services
    .AddGraphQLServer()
    // ... other configurations
    .AddErrorFilter<GlobalErrorFilter>();
```

### GraphQL-Step-12: Add Authentication & Authorization (Optional)

**TODO: Secure your GraphQL endpoints**

```csharp
// GraphQL/Queries/SecureQueries.cs
[ExtendObjectType(typeof(Query))]
public class SecureQueries
{
    [Authorize] // Requires authentication
    public async Task<User?> GetCurrentUser(ClaimsPrincipal user)
    {
        // Implementation
    }

    [Authorize(Policy = "AdminOnly")] // Requires specific policy
    public async Task<IEnumerable<User>> GetAllUsers()
    {
        // Implementation
    }
}
```

### GraphQL-Step-13: Add Data Loaders (Optional - Performance)

**TODO: Implement DataLoaders to solve N+1 problems**

```csharp
// GraphQL/DataLoaders/CategoryDataLoader.cs
public class CategoryDataLoader : BatchDataLoader<int, Category>
{
    private readonly IServiceProvider _serviceProvider;

    public CategoryDataLoader(
        IServiceProvider serviceProvider,
        IBatchScheduler batchScheduler)
        : base(batchScheduler)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<IReadOnlyDictionary<int, Category>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var categories = await context.Categories
            .Where(c => keys.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
            
        return categories;
    }
}

// Register in Program.cs
builder.Services.AddScoped<CategoryDataLoader>();
```

### GraphQL-Step-14: Testing Your GraphQL API

**TODO: Create tests and example queries**

#### Sample Queries:

```graphql
# Get all products
query GetProducts {
  products {
    id
    name
    description
    price
    category {
      name
    }
  }
}

# Get product by ID
query GetProduct($id: Int!) {
  productById(id: $id) {
    id
    name
    description
    price
    category {
      name
    }
  }
}

# Add a product
mutation AddProduct($input: AddProductInput!) {
  addProduct(input: $input) {
    id
    name
    price
  }
}

# Subscribe to product updates
subscription OnProductAdded {
  onProductAdded {
    id
    name
    price
  }
}
```

### GraphQL-Step-15: Add Validation

**TODO: Implement input validation**

```csharp
// GraphQL/Validators/AddProductInputValidator.cs
public class AddProductInputValidator : AbstractValidator<AddProductInput>
{
    public AddProductInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
            
        RuleFor(x => x.Price)
            .GreaterThan(0);
    }
}

// Register FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<AddProductInputValidator>();
```

### GraphQL-Step-16: Performance Optimization

**TODO: Optimize for production**

1. **Query Complexity Analysis**
```csharp
builder.Services
    .AddGraphQLServer()
    // ... other configurations
    .AddQueryRequestInterceptor<ComplexityAnalysisRequestInterceptor>()
    .ModifyRequestOptions(opt => opt.ComplexityCalculation = ComplexityCalculation.On);
```

2. **Query Depth Limiting**
```csharp
builder.Services
    .AddGraphQLServer()
    // ... other configurations
    .AddMaxExecutionDepthRule(10);
```

3. **Persisted Queries**
```csharp
builder.Services
    .AddGraphQLServer()
    // ... other configurations
    .UsePersistedQueryPipeline()
    .AddInMemoryQueryStorage();
```

### GraphQL-Step-17: Documentation and Schema Export

**TODO: Generate and maintain API documentation**

```bash
# Export GraphQL schema
dotnet run -- schema export --output schema.graphql

# Generate TypeScript types (if using frontend)
npm install -g graphql-code-generator
graphql-codegen
```

---

## Best Practices

1. **Use DataLoaders** for efficient data fetching
2. **Implement proper error handling** with custom error filters
3. **Add query complexity analysis** to prevent DoS attacks
4. **Use projections** to only select needed fields from database
5. **Implement proper logging** and monitoring
6. **Version your schema carefully** to avoid breaking changes
7. **Use strong typing** throughout your implementation
8. **Add comprehensive tests** for your resolvers

## Common Pitfalls to Avoid

- N+1 query problems (use DataLoaders)
- Exposing too much data (use authorization)
- Not limiting query complexity
- Poor error handling
- Not using projections with Entity Framework
- Ignoring performance implications of nested queries

## Resources

- [HotChocolate Documentation](https://chillicream.com/docs/hotchocolate)
- [GraphQL Specification](https://spec.graphql.org/)
- [GraphQL Best Practices](https://graphql.org/learn/best-practices/)

## Folder Structure

```
/GraphQL
  /Types
    ProductType.cs
    CategoryType.cs
  /Inputs
    ProductInput.cs
    CategoryInput.cs
  /Queries
    ProductQueries.cs
    CategoryQueries.cs
  /Mutations
    ProductMutations.cs
  /Subscriptions
    ProductSubscriptions.cs
  /DataLoaders
    CategoryDataLoader.cs
  /ErrorFilters
    GlobalErrorFilter.cs
  Query.cs
  Mutation.cs
  Subscription.cs
```