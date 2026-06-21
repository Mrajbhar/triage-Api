using Microsoft.AspNetCore.SignalR;
using triage.Hubs;

namespace triage.Services
{
   
    public interface IRealtimeNotifier
    {
        Task TicketChangedAsync(Guid tenantId, Guid ticketId, string kind, string message);
    }

    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<TicketsHub> _hub;
        private readonly ILogger<RealtimeNotifier> _log;

        public RealtimeNotifier(IHubContext<TicketsHub> hub, ILogger<RealtimeNotifier> log)
        {
            _hub = hub;
            _log = log;
        }

        public async Task TicketChangedAsync(Guid tenantId, Guid ticketId, string kind, string message)
        {
            try
            {
                await _hub.Clients
                    .Group(TicketsHub.GroupFor(tenantId))
                    .SendAsync("TicketChanged", new
                    {
                        ticketId,
                        kind,
                        message,
                        at = DateTime.UtcNow,
                    });
            }
            catch (Exception ex)
            {
             
                _log.LogWarning(ex, "Realtime push failed for ticket {TicketId} ({Kind}).", ticketId, kind);
            }
        }
    }
}