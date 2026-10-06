using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Models.ViewModels;

namespace TodoMVC.Controllers;

[Authorize(Roles = "admin")]
public class AdminController : Controller
{
    private readonly TodoDbContext _dbContext;
    private readonly ILogger<AdminController> _logger;

    public AdminController(TodoDbContext dbContext, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Reports));
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _dbContext.Users
            .Include(u => u.Credential)
            .Include(u => u.Todos)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserSummaryRow
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.Credential != null ? u.Credential.LastLoginAt : null,
                TotalTasks = u.Todos.Count,
                CompletedTasks = u.Todos.Count(t => t.Status == "Completed"),
                InProgressTasks = u.Todos.Count(t => t.Status == "InProgress"),
                CompletionRate = u.Todos.Count > 0 ? Math.Round((double)u.Todos.Count(t => t.Status == "Completed") / u.Todos.Count * 100, 1) : 0
            })
            .ToListAsync();

        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Reports(int? year, int? month)
    {
        var now = DateTime.Now;

        // Default to current year and all months if not specified
        var selectedYear = year ?? now.Year;
        var selectedMonth = month; // null means all months in the year

        // Find available years
        var distinctYears = await _dbContext.Todos
            .Select(t => t.StartTime.Year)
            .Distinct()
            .ToListAsync();

        if (!distinctYears.Contains(now.Year))
        {
            distinctYears.Add(now.Year);
        }
        var availableYears = distinctYears.OrderByDescending(y => y).ToList();

        // Query todos in selected period
        var query = _dbContext.Todos
            .Include(t => t.Category)
            .Include(t => t.User)
            .AsNoTracking();

        if (selectedYear > 0)
        {
            query = query.Where(t => t.StartTime.Year == selectedYear);
        }

        if (selectedMonth.HasValue && selectedMonth.Value >= 1 && selectedMonth.Value <= 12)
        {
            query = query.Where(t => t.StartTime.Month == selectedMonth.Value);
        }

        var todosInPeriod = await query.ToListAsync();
        var allCategories = await _dbContext.Categories.OrderBy(c => c.Name).ToListAsync();

        var totalTodos = todosInPeriod.Count;
        var completed = todosInPeriod.Count(t => t.Status == "Completed");
        var inProgress = todosInPeriod.Count(t => t.Status == "InProgress");
        var pending = todosInPeriod.Count(t => t.Status == "Pending");
        var hold = todosInPeriod.Count(t => t.Status == "Hold");
        var overallCompletionRate = totalTodos > 0 ? Math.Round((double)completed / totalTodos * 100, 1) : 0;
        var totalHours = Math.Round(todosInPeriod.Sum(t => (t.ExpectedEndTime - t.StartTime).TotalHours), 1);

        var totalUsers = await _dbContext.Users.CountAsync();
        var activeUsers = await _dbContext.Users.CountAsync(u => u.IsActive);

        // Category-based report rows
        var categoryReportRows = new List<CategoryReportRow>();

        foreach (var category in allCategories)
        {
            var catTodos = todosInPeriod.Where(t => t.CategoryId == category.Id).ToList();
            var catTotal = catTodos.Count;
            var catCompleted = catTodos.Count(t => t.Status == "Completed");
            var catInProgress = catTodos.Count(t => t.Status == "InProgress");
            var catPending = catTodos.Count(t => t.Status == "Pending");
            var catHold = catTodos.Count(t => t.Status == "Hold");
            var catRate = catTotal > 0 ? Math.Round((double)catCompleted / catTotal * 100, 1) : 0;
            var catHours = Math.Round(catTodos.Sum(t => (t.ExpectedEndTime - t.StartTime).TotalHours), 1);

            categoryReportRows.Add(new CategoryReportRow
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                ColorCode = category.ColorCode,
                Icon = category.Icon,
                TotalTodos = catTotal,
                Completed = catCompleted,
                InProgress = catInProgress,
                Pending = catPending,
                Hold = catHold,
                CompletionRate = catRate,
                TotalHours = catHours
            });
        }

        // User list summary for the report view
        var usersSummary = await _dbContext.Users
            .Include(u => u.Credential)
            .Include(u => u.Todos)
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummaryRow
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.Credential != null ? u.Credential.LastLoginAt : null,
                TotalTasks = u.Todos.Count,
                CompletedTasks = u.Todos.Count(t => t.Status == "Completed"),
                InProgressTasks = u.Todos.Count(t => t.Status == "InProgress"),
                CompletionRate = u.Todos.Count > 0 ? Math.Round((double)u.Todos.Count(t => t.Status == "Completed") / u.Todos.Count * 100, 1) : 0
            })
            .ToListAsync();

        var viewModel = new AdminKpiViewModel
        {
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            AvailableYears = availableYears,
            TotalUsersCount = totalUsers,
            ActiveUsersCount = activeUsers,
            TotalTodosInPeriod = totalTodos,
            CompletedTodosInPeriod = completed,
            InProgressTodosInPeriod = inProgress,
            PendingTodosInPeriod = pending,
            HoldTodosInPeriod = hold,
            OverallCompletionRate = overallCompletionRate,
            TotalHoursScheduledInPeriod = totalHours,
            CategoryReport = categoryReportRows,
            UsersList = usersSummary
        };

        return View(viewModel);
    }
}
