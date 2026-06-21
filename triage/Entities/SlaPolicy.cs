namespace triage.Entities
{
    public class SlaPolicy
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = "";
        public int ResponseMins { get; set; }
        public int ResolveMins { get; set; }
    }
}
