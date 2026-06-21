using Microsoft.EntityFrameworkCore;
using triage.Entities;
using triage.Services;

namespace triage.Data
{
    public class AppDbContext : DbContext
    {
       
        private readonly Guid _tenantId;

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
            : base(options)
        {
            _tenantId = currentUser.TenantId;
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<User> Users => Set<User>();
        public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketComment> TicketComments => Set<TicketComment>();
        public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();

        protected override void OnModelCreating(ModelBuilder b)
        {
          
            b.Entity<Tenant>().HasQueryFilter(t => t.Id == _tenantId);
            b.Entity<User>().HasQueryFilter(u => u.TenantId == _tenantId);
            b.Entity<SlaPolicy>().HasQueryFilter(s => s.TenantId == _tenantId);
            b.Entity<Ticket>().HasQueryFilter(t => t.TenantId == _tenantId);
            b.Entity<TicketComment>().HasQueryFilter(c => c.TenantId == _tenantId);
            b.Entity<TicketEvent>().HasQueryFilter(e => e.TenantId == _tenantId);

            b.Entity<User>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            b.Entity<User>().Property(u => u.Email).HasMaxLength(256);

            b.Entity<Ticket>().HasIndex(t => t.TenantId);
            b.Entity<Ticket>().HasIndex(t => new { t.TenantId, t.Status });
            b.Entity<Ticket>().Property(t => t.Subject).HasMaxLength(200);
        }
    }
}