namespace triage.Entities
{
    public class Ticket
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        public Guid RequesterId { get; set; }
        public Guid? AssigneeId { get; set; }

        public string Subject { get; set; } = "";
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Medium";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueAt { get; set; }
    }
}
