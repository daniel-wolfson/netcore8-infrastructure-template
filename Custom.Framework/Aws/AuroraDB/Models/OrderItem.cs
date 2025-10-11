using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Aws.AuroraDB.Models;

/// <summary>
/// Example entity: OrderItem
/// Demonstrates complex relationships and aggregations
/// </summary>
[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price", TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column("discount_amount", TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column("total_price", TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey(nameof(OrderId))]
    public virtual Order? Order { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }
}
