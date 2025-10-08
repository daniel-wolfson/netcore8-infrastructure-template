using Custom.Framework.GraphQL.Models;
using Microsoft.EntityFrameworkCore;

namespace Custom.Framework.GraphQL.Data
{
    /// <summary>
    /// Database context for GraphQL sample
    /// </summary>
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Price).HasPrecision(18, 2);

                entity.HasOne(e => e.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // Seed data
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and accessories" },
                new Category { Id = 2, Name = "Books", Description = "Books and publications" },
                new Category { Id = 3, Name = "Clothing", Description = "Apparel and fashion items" }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, CategoryId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Product { Id = 2, Name = "Smartphone", Description = "Latest smartphone model", Price = 699.99m, CategoryId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Product { Id = 3, Name = "C# Programming", Description = "Learn C# programming", Price = 49.99m, CategoryId = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
        }
    }
}
