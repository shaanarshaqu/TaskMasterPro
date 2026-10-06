using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoMVC.Models;

[Table("Todos")]
public class Todo
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime ExpectedEndTime { get; set; }

    /// <summary>
    /// Status: "Pending", "InProgress", "Hold", "Completed"
    /// </summary>
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Priority: "Low", "Medium", "High", "Urgent"
    /// </summary>
    [StringLength(20)]
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Indicates whether a start-time notification has been triggered for this task.
    /// </summary>
    public bool NotifiedStartTime { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(CategoryId))]
    public virtual Category Category { get; set; } = null!;
}
