using Custom.Framework.GraphQL.Data;
using Custom.Framework.GraphQL.Models;
using Custom.Framework.GraphQL.Mutations;
using Custom.Framework.GraphQL.Queries;
using Custom.Framework.GraphQL.Services;
using Custom.Framework.GraphQL.Subscriptions;
using Custom.Framework.GraphQL.Types;
using Custom.Framework.TestFactory.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Xunit.Abstractions;

namespace Custom.Framework.Tests
{
    /// <summary>
    /// Integration tests for GraphQL queries, mutations, and subscriptions.
    /// Tests the basic functionality of the GraphQL implementation.
    /// </summary>
    public class GraphQLTests(ITestOutputHelper output) : IAsyncLifetime
    {
        private WebApplicationFactory<TestProgram> _factory = default!;
        private ILogger _logger = default!;
        private HttpClient _client = default!;

        public async Task InitializeAsync()
        {
            _factory = new WebApplicationFactory<TestProgram>()
              .WithWebHostBuilder(builder =>
              {
                  builder.ConfigureServices((context, services) => ConfigureServices(services));
                  builder.ConfigureTestServices(services =>
                        services.AddSingleton<ILogger>(new TestLoggerWrapper(output)));
                  builder.Configure(app => ConfigureApp(app));
              });

            _client = _factory.CreateClient();
            _logger = _factory.Services.GetService<ILogger>()!;

            // Seed test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedTestDataAsync(dbContext);
        }

        #region Query Tests

        [Fact]
        public async Task Query_Hello_ShouldReturnGreeting()
        {
            // Arrange
            var query = new { query = "{ hello }" };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("Hello, GraphQL!", content);
            _logger.Information("Response: {Content}", content);
        }

        [Fact]
        public async Task Query_ServerTime_ShouldReturnDateTime()
        {
            // Arrange
            var query = new { query = "{ serverTime }" };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("serverTime", content);
            Assert.DoesNotContain("errors", content);
        }

        [Fact]
        public async Task Query_GetProducts_ShouldReturnAllProducts()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    products {
                        id
                        name
                        description
                        price
                        category {
                            id
                            name
                        }
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("products", content);
            Assert.Contains("Laptop", content);
            Assert.Contains("Electronics", content);
        }

        [Fact]
        public async Task Query_GetProductById_ShouldReturnSpecificProduct()
        {
            // Arrange
            var query = new
            {
                query = @"
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
                }",
                variables = new { id = 1 }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("productById", content);
            Assert.Contains("Laptop", content);
        }

        [Fact]
        public async Task Query_GetCategories_ShouldReturnAllCategories()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    categories {
                        id
                        name
                        description
                        products {
                            id
                            name
                        }
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("categories", content);
            Assert.Contains("Electronics", content);
            Assert.Contains("Books", content);
        }

        [Fact]
        public async Task Query_GetCategoryById_ShouldReturnSpecificCategory()
        {
            // Arrange
            var query = new
            {
                query = @"
                query GetCategory($id: Int!) {
                    categoryById(id: $id) {
                        id
                        name
                        description
                    }
                }",
                variables = new { id = 1 }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("categoryById", content);
            Assert.Contains("Electronics", content);
        }

        [Fact]
        public async Task Query_SearchProducts_ShouldReturnMatchingProducts()
        {
            // Arrange
            var query = new
            {
                query = @"
                query SearchProducts($searchTerm: String!) {
                    searchProducts(searchTerm: $searchTerm) {
                        id
                        name
                        description
                    }
                }",
                variables = new { searchTerm = "Laptop" }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("searchProducts", content);
            Assert.Contains("Laptop", content);
        }

        [Fact]
        public async Task Query_WithFiltering_ShouldReturnFilteredProducts()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    products(where: { price: { gte: 500 } }) {
                        id
                        name
                        price
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("products", content);
        }

        [Fact]
        public async Task Query_WithSorting_ShouldReturnSortedProducts()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    products(order: { price: DESC }) {
                        id
                        name
                        price
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("products", content);
        }

        #endregion

        #region Mutation Tests

        [Fact]
        public async Task Mutation_AddProduct_ShouldCreateNewProduct()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                mutation AddProduct($input: AddProductInput!) {
                    addProduct(input: $input) {
                        id
                        name
                        description
                        price
                        category {
                            id
                            name
                        }
                    }
                }",
                variables = new
                {
                    input = new
                    {
                        name = "Test Product",
                        description = "Test Description",
                        price = 199.99,
                        categoryId = 1
                    }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("addProduct", content);
            Assert.Contains("Test Product", content);
            Assert.Contains("199.99", content);
        }

        [Fact]
        public async Task Mutation_UpdateProduct_ShouldModifyExistingProduct()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                mutation UpdateProduct($input: UpdateProductInput!) {
                    updateProduct(input: $input) {
                        id
                        name
                        description
                        price
                    }
                }",
                variables = new
                {
                    input = new
                    {
                        id = 1,
                        name = "Updated Laptop",
                        price = 1199.99
                    }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("updateProduct", content);
            Assert.Contains("Updated Laptop", content);
        }

        [Fact]
        public async Task Mutation_DeleteProduct_ShouldRemoveProduct()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                mutation DeleteProduct($id: Int!) {
                    deleteProduct(id: $id)
                }",
                variables = new { id = 3 }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("deleteProduct", content);
            Assert.Contains("true", content);
        }

        [Fact]
        public async Task Mutation_AddCategory_ShouldCreateNewCategory()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                mutation AddCategory($input: AddCategoryInput!) {
                    addCategory(input: $input) {
                        id
                        name
                        description
                    }
                }",
                variables = new
                {
                    input = new
                    {
                        name = "Test Category",
                        description = "Test Category Description"
                    }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("addCategory", content);
            Assert.Contains("Test Category", content);
        }

        [Fact]
        public async Task Mutation_UpdateCategory_ShouldModifyExistingCategory()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                    mutation UpdateCategory($input: UpdateCategoryInput!) {
                        updateCategory(input: $input) {
                            id
                            name
                            description
                        }
                    }",
                variables = new
                {
                    input = new
                    {
                        id = 1,
                        name = "Updated Electronics",
                        description = "Updated description"
                    }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("updateCategory", content);
            Assert.Contains("Updated Electronics", content);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Query_InvalidProductId_ShouldReturnNull()
        {
            // Arrange
            var query = new
            {
                query = @"
                    query GetProduct($id: Int!) {
                        productById(id: $id) {
                            id
                            name
                        }
                    }",
                variables = new { id = 99999 }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("null", content);
        }

        [Fact]
        public async Task Query_InvalidSyntax_ShouldReturnError()
        {
            // Arrange
            var query = new
            {
                query = "{ invalid syntax here }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("errors", content);
        }

        [Fact]
        public async Task Mutation_DeleteNonExistentProduct_ShouldReturnFalse()
        {
            // Arrange
            var mutation = new
            {
                query = @"
                mutation DeleteProduct($id: Int!) {
                    deleteProduct(id: $id)
                }",
                variables = new { id = 99999 }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", mutation);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("false", content);
        }

        #endregion

        #region Complex Query Tests

        [Fact]
        public async Task Query_NestedRelationships_ShouldReturnCompleteData()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    categories {
                        id
                        name
                        products {
                            id
                            name
                            price
                            category {
                                id
                                name
                            }
                        }
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("categories", content);
            Assert.Contains("products", content);
            Assert.Contains("category", content);
        }

        [Fact]
        public async Task Query_MultipleOperations_ShouldReturnAllData()
        {
            // Arrange
            var query = new
            {
                query = @"
                {
                    hello
                    serverTime
                    products {
                        id
                        name
                    }
                    categories {
                        id
                        name
                    }
                }"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/graphql", query);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"Response: {content}");

            Assert.Contains("hello", content);
            Assert.Contains("serverTime", content);
            Assert.Contains("products", content);
            Assert.Contains("categories", content);
        }

        public Task DisposeAsync()
        {
            _client?.Dispose();
            return Task.CompletedTask;
        }

        #endregion

        #region private methods

        private async Task SeedTestDataAsync(ApplicationDbContext dbContext)
        {
            // Clear existing data
            dbContext.Products.RemoveRange(dbContext.Products);
            dbContext.Categories.RemoveRange(dbContext.Categories);
            await dbContext.SaveChangesAsync();

            // Seed categories
            var categories = new[]
            {
                new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and accessories" },
                new Category { Id = 2, Name = "Books", Description = "Books and publications" },
                new Category { Id = 3, Name = "Clothing", Description = "Apparel and fashion items" }
            };

            dbContext.Categories.AddRange(categories);
            await dbContext.SaveChangesAsync();

            // Seed products
            var products = new[]
            {
                new Product
                {
                    Id = 1,
                    Name = "Laptop",
                    Description = "High-performance laptop",
                    Price = 999.99m,
                    CategoryId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Smartphone",
                    Description = "Latest smartphone model",
                    Price = 699.99m,
                    CategoryId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "C# Programming",
                    Description = "Learn C# programming",
                    Price = 49.99m,
                    CategoryId = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            dbContext.Products.AddRange(products);
            await dbContext.SaveChangesAsync();
        }

        private Action<WebHostBuilderContext, IServiceCollection> ConfigureServices1()
        {
            return (context, services) =>
            {
                services.AddCors(options =>
                {
                    options.AddDefaultPolicy(policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("GraphQLTests"));

                services.AddScoped<IProductService, ProductService>();
                services.AddScoped<ICategoryService, CategoryService>();

                services
                    .AddGraphQLServer()
                    .AddQueryType<Query>()
                    .AddMutationType<Mutation>()
                    .AddSubscriptionType<Subscription>()
                    .AddTypeExtension<ProductQueries>()
                    .AddTypeExtension<CategoryQueries>()
                    .AddTypeExtension<ProductMutations>()
                    .AddTypeExtension<CategoryMutations>()
                    .AddTypeExtension<ProductSubscriptions>()
                    .AddTypeExtension<CategorySubscriptions>()
                    .AddType<ProductType>()
                    .AddType<CategoryType>()
                    .AddFiltering()
                    .AddSorting()
                    .AddProjections()
                    .AddInMemorySubscriptions();
            };
        }

        private void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseCors();
            app.UseWebSockets();          // needed for subscriptions
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGraphQL("/graphql");
            });
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("GraphQLTests"));

            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICategoryService, CategoryService>();

            services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddMutationType<Mutation>()
                .AddSubscriptionType<Subscription>()
                .AddTypeExtension<ProductQueries>()
                .AddTypeExtension<CategoryQueries>()
                .AddTypeExtension<ProductMutations>()
                .AddTypeExtension<CategoryMutations>()
                .AddTypeExtension<ProductSubscriptions>()
                .AddTypeExtension<CategorySubscriptions>()
                .AddType<ProductType>()
                .AddType<CategoryType>()
                .AddFiltering()
                .AddSorting()
                .AddProjections()
                .AddInMemorySubscriptions();
        }

        #endregion
    }
}
