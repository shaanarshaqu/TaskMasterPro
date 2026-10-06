using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoMVC.Models;

[Table("Categories")]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string ColorCode { get; set; } = "#6366F1"; // Modern Indigo default

    [StringLength(50)]
    public string Icon { get; set; } = "bi-folder";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ICollection<Todo> Todos { get; set; } = new List<Todo>();
}
