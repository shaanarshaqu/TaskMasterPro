using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Models;

namespace TodoMVC.Services;

public record TimeOverlapResult(
    bool HasOverlap,
    string? Message = null,
    int? ConflictingTodoId = null,
    string? ConflictingTitle = null,
    DateTime? ConflictingStart = null,
    DateTime? ConflictingEnd = null
);

public interface ITodoService
{
    Task<TimeOverlapResult> CheckTimeOverlapAsync(int userId, DateTime startTime, DateTime expectedEndTime, int? excludeTodoId = null);
}

public class TodoService : ITodoService
{
    private readonly TodoDbContext _dbContext;
    private readonly ILogger<TodoService> _logger;

    public TodoService(TodoDbContext dbContext, ILogger<TodoService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TimeOverlapResult> CheckTimeOverlapAsync(int userId, DateTime startTime, DateTime expectedEndTime, int? excludeTodoId = null)
    {
        if (expectedEndTime <= startTime)
        {
            return new TimeOverlapResult(
                HasOverlap: true,
                Message: "Expected End Time must be later than Start Time."
            );
        }

        // Check if existing todo for this user overlaps
        // Two intervals [A, B] and [C, D] overlap if A < D and B > C
        var query = _dbContext.Todos
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (excludeTodoId.HasValue && excludeTodoId.Value > 0)
        {
            query = query.Where(t => t.Id != excludeTodoId.Value);
        }

        var conflictingTodo = await query
            .Where(t => t.StartTime < expectedEndTime && t.ExpectedEndTime > startTime)
            .OrderBy(t => t.StartTime)
            .FirstOrDefaultAsync();

        if (conflictingTodo != null)
        {
            var msg = $"Time slot conflict! You already have task '{conflictingTodo.Title}' scheduled from {conflictingTodo.StartTime:yyyy-MM-dd HH:mm} to {conflictingTodo.ExpectedEndTime:yyyy-MM-dd HH:mm}. Tasks cannot overlap.";
            return new TimeOverlapResult(
                HasOverlap: true,
                Message: msg,
                ConflictingTodoId: conflictingTodo.Id,
                ConflictingTitle: conflictingTodo.Title,
                ConflictingStart: conflictingTodo.StartTime,
                ConflictingEnd: conflictingTodo.ExpectedEndTime
            );
        }

        return new TimeOverlapResult(HasOverlap: false);
    }
}
