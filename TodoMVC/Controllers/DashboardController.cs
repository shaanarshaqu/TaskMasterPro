using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Models.ViewModels;

namespace TodoMVC.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly TodoDbContext _dbContext;

    public DashboardController(TodoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        var now = DateTime.Now;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var todos = await _dbContext.Todos
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.StartTime)
            .ToListAsync();

        var total = todos.Count;
        var completed = todos.Count(t => t.Status == "Completed");
        var inProgress = todos.Count(t => t.Status == "InProgress");
        var pending = todos.Count(t => t.Status == "Pending");
        var hold = todos.Count(t => t.Status == "Hold");
        var overdue = todos.Count(t => t.ExpectedEndTime < now && t.Status != "Completed");
        var completionRate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0;
        var totalScheduledHours = Math.Round(todos.Sum(t => (t.ExpectedEndTime - t.StartTime).TotalHours), 1);

        var todayTodos = todos
            .Where(t => t.StartTime >= todayStart && t.StartTime < todayEnd)
            .Select(t => new TodoItemViewModel
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                CategoryColor = t.Category.ColorCode,
                CategoryIcon = t.Category.Icon,
                StartTime = t.StartTime,
                ExpectedEndTime = t.ExpectedEndTime,
                Status = t.Status,
                Priority = t.Priority
            })
            .ToList();

        var upcomingTodos = todos
            .Where(t => t.StartTime >= now && t.Status != "Completed")
            .Take(5)
            .Select(t => new TodoItemViewModel
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                CategoryColor = t.Category.ColorCode,
                CategoryIcon = t.Category.Icon,
                StartTime = t.StartTime,
                ExpectedEndTime = t.ExpectedEndTime,
                Status = t.Status,
                Priority = t.Priority
            })
            .ToList();

        var categoryBreakdown = todos
            .GroupBy(t => t.Category)
            .Select(g => new CategoryStatItem
            {
                CategoryName = g.Key.Name,
                ColorCode = g.Key.ColorCode,
                Count = g.Count(),
                Percentage = total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0
            })
            .OrderByDescending(c => c.Count)
            .ToList();

        var viewModel = new UserKpiViewModel
        {
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            TotalTodos = total,
            CompletedTodos = completed,
            InProgressTodos = inProgress,
            PendingTodos = pending,
            HoldTodos = hold,
            OverdueTodos = overdue,
            CompletionRate = completionRate,
            TotalScheduledHours = totalScheduledHours,
            TodayTodos = todayTodos,
            UpcomingTodos = upcomingTodos,
            CategoryBreakdown = categoryBreakdown
        };

        return View(viewModel);
    }
}
