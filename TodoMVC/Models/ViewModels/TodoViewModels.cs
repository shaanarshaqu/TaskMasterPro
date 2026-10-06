using System.ComponentModel.DataAnnotations;
using TodoMVC.Models;

namespace TodoMVC.Models.ViewModels;

public class TodoItemViewModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#4F46E5";
    public string CategoryIcon { get; set; } = "bi-folder";

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime ExpectedEndTime { get; set; }
    public string Status { get; set; } = "Pending";
    public string Priority { get; set; } = "Medium";

    public bool IsActiveNow => DateTime.Now >= StartTime && DateTime.Now <= ExpectedEndTime;
    public bool IsOverdue => DateTime.Now > ExpectedEndTime && Status != "Completed";
    public double DurationHours => (ExpectedEndTime - StartTime).TotalHours;
}

public class TodoListViewModel
{
    public List<TodoItemViewModel> Todos { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<User> AllUsers { get; set; } = new();

    // Filters
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? CategoryId { get; set; }
    public string? Priority { get; set; }
    public int? SelectedUserId { get; set; }
    public string ViewMode { get; set; } = "list"; // "list" or "board"

    // Context flags
    public bool IsAdmin { get; set; }
    public int CurrentUserId { get; set; }
    public string CurrentUserRole { get; set; } = "user";
}

public class TodoInputModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Start Time is required")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "Expected End Time is required")]
    public DateTime ExpectedEndTime { get; set; }

    [Required(ErrorMessage = "Status is required")]
    public string Status { get; set; } = "Pending";

    [Required(ErrorMessage = "Priority is required")]
    public string Priority { get; set; } = "Medium";

    public int? AssignedUserId { get; set; }
}
