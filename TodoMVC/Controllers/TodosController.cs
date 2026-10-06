using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Models;
using TodoMVC.Models.ViewModels;
using TodoMVC.Services;

namespace TodoMVC.Controllers;

[Authorize]
public class TodosController : Controller
{
    private readonly TodoDbContext _dbContext;
    private readonly ITodoService _todoService;
    private readonly ILogger<TodosController> _logger;

    public TodosController(
        TodoDbContext dbContext,
        ITodoService todoService,
        ILogger<TodosController> logger)
    {
        _dbContext = dbContext;
        _todoService = todoService;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("admin");

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        int? categoryId,
        string? priority,
        int? userId,
        string viewMode = "list")
    {
        var currentUserId = CurrentUserId;
        var isAdmin = IsAdmin;

        var query = _dbContext.Todos
            .Include(t => t.Category)
            .Include(t => t.User)
            .AsNoTracking();

        // Role-based visibility
        if (isAdmin)
        {
            // Admin can view all users, or filter by a specific user
            if (userId.HasValue && userId.Value > 0)
            {
                query = query.Where(t => t.UserId == userId.Value);
            }
        }
        else
        {
            // Regular user can only view their own todos
            query = query.Where(t => t.UserId == currentUserId);
        }

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(searchClean) ||
                                     (t.Description != null && t.Description.ToLower().Contains(searchClean)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Status == status);
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(priority) && !priority.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Priority == priority);
        }

        var todos = await query
            .OrderBy(t => t.StartTime)
            .Select(t => new TodoItemViewModel
            {
                Id = t.Id,
                UserId = t.UserId,
                Username = t.User.Username,
                FullName = t.User.FullName,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                CategoryColor = t.Category.ColorCode,
                CategoryIcon = t.Category.Icon,
                Title = t.Title,
                Description = t.Description,
                StartTime = t.StartTime,
                ExpectedEndTime = t.ExpectedEndTime,
                Status = t.Status,
                Priority = t.Priority
            })
            .ToListAsync();

        var categories = await _dbContext.Categories.OrderBy(c => c.Name).ToListAsync();
        var allUsers = isAdmin ? await _dbContext.Users.OrderBy(u => u.FullName).ToListAsync() : new List<User>();

        var model = new TodoListViewModel
        {
            Todos = todos,
            Categories = categories,
            AllUsers = allUsers,
            Search = search,
            Status = status,
            CategoryId = categoryId,
            Priority = priority,
            SelectedUserId = userId,
            ViewMode = viewMode,
            IsAdmin = isAdmin,
            CurrentUserId = currentUserId,
            CurrentUserRole = isAdmin ? "admin" : "user"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] TodoInputModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Json(new { success = false, message = errors });
        }

        var targetUserId = CurrentUserId;
        if (IsAdmin && model.AssignedUserId.HasValue && model.AssignedUserId.Value > 0)
        {
            targetUserId = model.AssignedUserId.Value;
        }

        // Validate time overlap
        var overlapCheck = await _todoService.CheckTimeOverlapAsync(targetUserId, model.StartTime, model.ExpectedEndTime);
        if (overlapCheck.HasOverlap)
        {
            return Json(new
            {
                success = false,
                isOverlapError = true,
                message = overlapCheck.Message,
                conflictingTitle = overlapCheck.ConflictingTitle,
                conflictingStart = overlapCheck.ConflictingStart?.ToString("yyyy-MM-dd HH:mm"),
                conflictingEnd = overlapCheck.ConflictingEnd?.ToString("yyyy-MM-dd HH:mm")
            });
        }

        var now = DateTime.Now;
        var newTodo = new Todo
        {
            UserId = targetUserId,
            CategoryId = model.CategoryId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            StartTime = model.StartTime,
            ExpectedEndTime = model.ExpectedEndTime,
            Status = model.Status,
            Priority = model.Priority,
            NotifiedStartTime = model.StartTime <= now, // if start time is already in past/now, flag it
            CreatedAt = DateTime.UtcNow
        };

        if (model.Status == "Completed")
        {
            newTodo.CompletedAt = DateTime.UtcNow;
        }

        _dbContext.Todos.Add(newTodo);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Task created: {Title} by User {UserId}", newTodo.Title, targetUserId);

        return Json(new
        {
            success = true,
            message = "Task created successfully!",
            todoId = newTodo.Id
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTodo(int id)
    {
        var todo = await _dbContext.Todos
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (todo == null)
        {
            return NotFound(new { success = false, message = "Task not found." });
        }

        if (!IsAdmin && todo.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return Json(new
        {
            success = true,
            id = todo.Id,
            title = todo.Title,
            description = todo.Description,
            categoryId = todo.CategoryId,
            startTime = todo.StartTime.ToString("yyyy-MM-ddTHH:mm"),
            expectedEndTime = todo.ExpectedEndTime.ToString("yyyy-MM-ddTHH:mm"),
            status = todo.Status,
            priority = todo.Priority,
            assignedUserId = todo.UserId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] TodoInputModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Json(new { success = false, message = errors });
        }

        var todo = await _dbContext.Todos.FirstOrDefaultAsync(t => t.Id == model.Id);
        if (todo == null)
        {
            return Json(new { success = false, message = "Task not found." });
        }

        if (!IsAdmin && todo.UserId != CurrentUserId)
        {
            return Json(new { success = false, message = "Unauthorized to edit this task." });
        }

        var targetUserId = todo.UserId;
        if (IsAdmin && model.AssignedUserId.HasValue && model.AssignedUserId.Value > 0)
        {
            targetUserId = model.AssignedUserId.Value;
            todo.UserId = targetUserId;
        }

        // Validate time overlap (excluding this task)
        var overlapCheck = await _todoService.CheckTimeOverlapAsync(targetUserId, model.StartTime, model.ExpectedEndTime, model.Id);
        if (overlapCheck.HasOverlap)
        {
            return Json(new
            {
                success = false,
                isOverlapError = true,
                message = overlapCheck.Message,
                conflictingTitle = overlapCheck.ConflictingTitle,
                conflictingStart = overlapCheck.ConflictingStart?.ToString("yyyy-MM-dd HH:mm"),
                conflictingEnd = overlapCheck.ConflictingEnd?.ToString("yyyy-MM-dd HH:mm")
            });
        }

        // Check if start time changed and is in future, reset notification flag
        if (todo.StartTime != model.StartTime && model.StartTime > DateTime.Now)
        {
            todo.NotifiedStartTime = false;
        }

        todo.Title = model.Title.Trim();
        todo.Description = model.Description?.Trim();
        todo.CategoryId = model.CategoryId;
        todo.StartTime = model.StartTime;
        todo.ExpectedEndTime = model.ExpectedEndTime;
        todo.Priority = model.Priority;

        // If status changed
        if (todo.Status != model.Status)
        {
            todo.Status = model.Status;
            if (model.Status == "Completed")
            {
                todo.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                todo.CompletedAt = null;
            }
        }

        todo.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Task updated: {Title} (ID: {Id})", todo.Title, todo.Id);

        return Json(new
        {
            success = true,
            message = "Task updated successfully!"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var validStatuses = new[] { "Pending", "InProgress", "Hold", "Completed" };
        if (!validStatuses.Contains(status))
        {
            return Json(new { success = false, message = "Invalid status." });
        }

        var todo = await _dbContext.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null)
        {
            return Json(new { success = false, message = "Task not found." });
        }

        if (!IsAdmin && todo.UserId != CurrentUserId)
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        todo.Status = status;
        if (status == "Completed")
        {
            todo.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            todo.CompletedAt = null;
        }
        todo.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            status = todo.Status,
            message = $"Task status updated to '{status}'"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var todo = await _dbContext.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null)
        {
            return Json(new { success = false, message = "Task not found." });
        }

        if (!IsAdmin && todo.UserId != CurrentUserId)
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        _dbContext.Todos.Remove(todo);
        await _dbContext.SaveChangesAsync();

        return Json(new { success = true, message = "Task deleted successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> CheckOverlap(DateTime startTime, DateTime expectedEndTime, int? excludeId = null, int? targetUserId = null)
    {
        var checkUserId = CurrentUserId;
        if (IsAdmin && targetUserId.HasValue && targetUserId.Value > 0)
        {
            checkUserId = targetUserId.Value;
        }

        var result = await _todoService.CheckTimeOverlapAsync(checkUserId, startTime, expectedEndTime, excludeId);

        return Json(new
        {
            hasOverlap = result.HasOverlap,
            message = result.Message,
            conflictingTitle = result.ConflictingTitle,
            conflictingStart = result.ConflictingStart?.ToString("yyyy-MM-dd HH:mm"),
            conflictingEnd = result.ConflictingEnd?.ToString("yyyy-MM-dd HH:mm")
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveAlerts()
    {
        var userId = CurrentUserId;
        var now = DateTime.Now;

        // Tasks currently active and not yet completed
        var activeAlerts = await _dbContext.Todos
            .Include(t => t.Category)
            .Where(t => t.UserId == userId &&
                        t.Status != "Completed" &&
                        t.StartTime <= now &&
                        t.ExpectedEndTime >= now.AddMinutes(-30))
            .OrderBy(t => t.StartTime)
            .Select(t => new
            {
                id = t.Id,
                title = t.Title,
                description = t.Description,
                startTime = t.StartTime.ToString("yyyy-MM-dd HH:mm"),
                expectedEndTime = t.ExpectedEndTime.ToString("yyyy-MM-dd HH:mm"),
                category = t.Category.Name,
                categoryColor = t.Category.ColorCode,
                priority = t.Priority,
                status = t.Status
            })
            .ToListAsync();

        return Json(activeAlerts);
    }
}
