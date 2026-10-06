using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Hubs;

namespace TodoMVC.Services;

public class TodoNotificationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TodoNotificationWorker> _logger;

    public TodoNotificationWorker(
        IServiceProvider serviceProvider,
        ILogger<TodoNotificationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TodoNotificationWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TodoNotificationHub>>();

                var now = DateTime.Now;

                // Find todos whose start time has arrived (StartTime <= now && ExpectedEndTime >= now)
                // and has not yet been notified, and is not yet completed
                var tasksToAlert = await dbContext.Todos
                    .Include(t => t.Category)
                    .Include(t => t.User)
                    .Where(t => !t.NotifiedStartTime &&
                                t.Status != "Completed" &&
                                t.StartTime <= now &&
                                t.ExpectedEndTime >= now.AddMinutes(-30)) // Catch tasks started up to 30 min ago if server was idle
                    .ToListAsync(stoppingToken);

                if (tasksToAlert.Count > 0)
                {
                    _logger.LogInformation("Found {Count} tasks whose start time has arrived.", tasksToAlert.Count);

                    foreach (var todo in tasksToAlert)
                    {
                        todo.NotifiedStartTime = true;

                        var alertPayload = new
                        {
                            id = todo.Id,
                            title = todo.Title,
                            description = todo.Description,
                            startTime = todo.StartTime.ToString("yyyy-MM-dd HH:mm"),
                            expectedEndTime = todo.ExpectedEndTime.ToString("yyyy-MM-dd HH:mm"),
                            category = todo.Category?.Name ?? "General",
                            categoryColor = todo.Category?.ColorCode ?? "#4f46e5",
                            priority = todo.Priority,
                            status = todo.Status,
                            message = $"Time to start! Task '{todo.Title}' is scheduled to start right now."
                        };

                        // Send real-time alert to user's SignalR group
                        await hubContext.Clients.Group($"user_{todo.UserId}")
                            .SendAsync("ReceiveTodoStartAlert", alertPayload, stoppingToken);

                        _logger.LogInformation("Dispatched start alert for Todo '{Title}' (ID: {Id}) to User {UserId}",
                            todo.Title, todo.Id, todo.UserId);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking for due todo alerts in TodoNotificationWorker");
            }
        }

        _logger.LogInformation("TodoNotificationWorker stopped.");
    }
}
