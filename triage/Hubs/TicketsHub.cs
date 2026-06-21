using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace triage.Hubs
{
   
    [Authorize]
    public class TicketsHub : Hub
    {
        public static string GroupFor(Guid tenantId) => $"tenant:{tenantId}";

        public override async Task OnConnectedAsync()
        {
            var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(Guid.Parse(tenantId)));

            await base.OnConnectedAsync();
        }
    }
}