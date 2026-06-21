using Microsoft.EntityFrameworkCore;
using triage.Data;
using triage.Entities;

namespace triage.Workers
{
   
    public class SlaEscalationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SlaEscalationWorker> _log;

        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan WarningWindow = TimeSpan.FromMinutes(30);

        public SlaEscalationWorker(IServiceScopeFactory scopeFactory,
                                   ILogger<SlaEscalationWorker> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("SLA escalation worker started (every {s}s, window {w}m).",
                ScanInterval.TotalSeconds, WarningWindow.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "SLA scan failed.");
                }

                try { await Task.Delay(ScanInterval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task ScanOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifier = scope.ServiceProvider.GetRequiredService<triage.Services.IRealtimeNotifier>();

            var now = DateTime.UtcNow;
            var cutoff = now.Add(WarningWindow);

            
            var candidates = await db.Tickets
                .IgnoreQueryFilters()
                .Where(t => t.DueAt != null
                            && t.DueAt <= cutoff
                            && t.Status != "Resolved")
                .Select(t => new { t.Id, t.TenantId, t.DueAt })
                .ToListAsync(ct);

            if (candidates.Count == 0) return;

           
            var ticketIds = candidates.Select(c => c.Id).ToList();
            var alreadyEscalated = (await db.TicketEvents
                .IgnoreQueryFilters()
                .Where(e => ticketIds.Contains(e.TicketId) && e.EventType == "Escalated")
                .Select(e => e.TicketId)
                .ToListAsync(ct))
                .ToHashSet();

            var newEvents = new List<TicketEvent>();
            foreach (var c in candidates)
            {
                if (alreadyEscalated.Contains(c.Id)) continue;

                var minsLeft = (int)Math.Max(0, (c.DueAt!.Value - now).TotalMinutes);
                newEvents.Add(new TicketEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = c.TenantId,    
                    TicketId = c.Id,
                    ActorId = null,            
                    EventType = "Escalated",
                    Detail = minsLeft > 0
                        ? $"SLA breach imminent — {minsLeft} minute(s) left."
                        : "SLA breached — ticket is overdue.",
                    CreatedAt = now,
                });
            }

            if (newEvents.Count == 0) return;

            db.TicketEvents.AddRange(newEvents);
            await db.SaveChangesAsync(ct);

            foreach (var e in newEvents)
                await notifier.TicketChangedAsync(e.TenantId, e.TicketId, "escalated", e.Detail ?? "SLA breach imminent");

            _log.LogInformation("Logged {Count} SLA escalation event(s).", newEvents.Count);

        }
    }
}