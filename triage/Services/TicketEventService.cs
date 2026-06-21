using triage.Data;
using triage.Entities;

namespace triage.Services
{
   
    public interface ITicketEventService
    {
        void Track(Guid ticketId, string eventType, string? detail = null);
    }

    public class TicketEventService : ITicketEventService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _me;

        public TicketEventService(AppDbContext db, ICurrentUser me)
        {
            _db = db;
            _me = me;
        }

        public void Track(Guid ticketId, string eventType, string? detail = null)
        {
            _db.TicketEvents.Add(new TicketEvent
            {
                Id = Guid.NewGuid(),
                TenantId = _me.TenantId,
                TicketId = ticketId,
                ActorId = _me.UserId == Guid.Empty ? null : _me.UserId,
                EventType = eventType,
                Detail = detail,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }
}