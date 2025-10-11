using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Aws.AuroraDB.Models;

/// <summary>
/// Example entity: Product
/// Demonstrates inventory management and concurrent updates
/// </summary>
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("cost", TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    [Column("reorder_threshold")]
    public int ReorderThreshold { get; set; } = 10;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Computed property
    [NotMapped]
    public int AvailableQuantity => Quantity - ReservedQuantity;

    [NotMapped]
    public bool IsLowStock => AvailableQuantity <= ReorderThreshold;

    // Navigation property
    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];
}
