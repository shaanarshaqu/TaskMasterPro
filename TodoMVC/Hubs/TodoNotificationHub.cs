using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TodoMVC.Hubs;

public class TodoNotificationHub : Hub
{
    private readonly ILogger<TodoNotificationHub> _logger;

    public TodoNotificationHub(ILogger<TodoNotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("SignalR: Connected user {UserId} (ConnectionId: {ConnId})", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public async Task RegisterUser(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("SignalR: User explicitly registered group user_{UserId}", userId);
        }
    }
}
