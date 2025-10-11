using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Aws.AuroraDB.Models;

/// <summary>
/// Example entity: Customer
/// Demonstrates basic CRUD operations with Aurora
/// </summary>
[Table("customers")]
public class Customer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("login_count")]
    public int LoginCount { get; set; }

    // Navigation property
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public string GetFullName() => $"{FirstName} {LastName}";
}
