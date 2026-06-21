namespace triage.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string PlanTier { get; set; } = "Free";   
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
