using TodoMVC.Models;

namespace TodoMVC.Models.ViewModels;

public class UserKpiViewModel
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";

    // KPI Counters
    public int TotalTodos { get; set; }
    public int CompletedTodos { get; set; }
    public int InProgressTodos { get; set; }
    public int PendingTodos { get; set; }
    public int HoldTodos { get; set; }
    public int OverdueTodos { get; set; }
    public double CompletionRate { get; set; }
    public double TotalScheduledHours { get; set; }

    // Upcoming and Today
    public List<TodoItemViewModel> TodayTodos { get; set; } = new();
    public List<TodoItemViewModel> UpcomingTodos { get; set; } = new();
    public List<CategoryStatItem> CategoryBreakdown { get; set; } = new();
}

public class AdminKpiViewModel
{
    // Filter controls
    public int? SelectedYear { get; set; }
    public int? SelectedMonth { get; set; }
    public List<int> AvailableYears { get; set; } = new();

    // High Level KPIs
    public int TotalUsersCount { get; set; }
    public int ActiveUsersCount { get; set; }
    public int TotalTodosInPeriod { get; set; }
    public int CompletedTodosInPeriod { get; set; }
    public int InProgressTodosInPeriod { get; set; }
    public int PendingTodosInPeriod { get; set; }
    public int HoldTodosInPeriod { get; set; }
    public double OverallCompletionRate { get; set; }
    public double TotalHoursScheduledInPeriod { get; set; }

    // Category Report
    public List<CategoryReportRow> CategoryReport { get; set; } = new();

    // Users Directory & Productivity
    public List<UserSummaryRow> UsersList { get; set; } = new();
}

public class CategoryReportRow
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ColorCode { get; set; } = "#4F46E5";
    public string Icon { get; set; } = "bi-folder";
    public int TotalTodos { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Pending { get; set; }
    public int Hold { get; set; }
    public double CompletionRate { get; set; }
    public double TotalHours { get; set; }
}

public class UserSummaryRow
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public double CompletionRate { get; set; }
}

public class CategoryStatItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string ColorCode { get; set; } = "#4F46E5";
    public int Count { get; set; }
    public double Percentage { get; set; }
}
