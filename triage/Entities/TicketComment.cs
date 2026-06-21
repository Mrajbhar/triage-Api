namespace triage.Entities
{
    public class TicketComment
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid TicketId { get; set; }
        public Guid AuthorId { get; set; }
        public string Body { get; set; } = "";
        public bool IsInternal { get; set; }             
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
