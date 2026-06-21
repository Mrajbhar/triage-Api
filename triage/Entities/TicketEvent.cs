namespace triage.Entities
{
    public class TicketEvent
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid TicketId { get; set; }
        public Guid? ActorId { get; set; }   
        public string EventType { get; set; } = "";  
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}