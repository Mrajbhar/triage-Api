using System.Security.Claims;

namespace triage.Services
{
    public interface ICurrentUser
    {
        Guid TenantId { get; }
        Guid UserId { get; }
        string? Role { get; }
        bool IsAuthenticated { get; }
    }

    public class CurrentUser : ICurrentUser
    {
        public Guid TenantId { get; }
        public Guid UserId { get; }
        public string? Role { get; }
        public bool IsAuthenticated { get; }

        public CurrentUser(IHttpContextAccessor accessor)
        {
            var user = accessor.HttpContext?.User;
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

            if (!IsAuthenticated) return;

            
            UserId = ParseGuid(
                user!.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value);

            TenantId = ParseGuid(user.FindFirst("tenant_id")?.Value);

            Role = user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value;
        }

        private static Guid ParseGuid(string? value) =>
            Guid.TryParse(value, out var g) ? g : Guid.Empty;
    }
}
