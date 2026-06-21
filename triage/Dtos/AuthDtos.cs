using System.ComponentModel.DataAnnotations;

namespace triage.Dtos
{
    
    public record RegisterRequest(
        [Required, StringLength(150, MinimumLength = 1)] string CompanyName,
        [Required, StringLength(150, MinimumLength = 1)] string FullName,
        [Required, EmailAddress, StringLength(256)] string Email,
        [Required, MinLength(6), StringLength(100)] string Password);

    public record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required] string Password);

    
    public record AuthResponse(
        string Token,
        Guid UserId,
        string FullName,
        string Email,
        string Role,
        Guid TenantId);

   
    public record MeResponse(
        Guid UserId,
        string FullName,
        string Email,
        string Role,
        Guid TenantId,
        string Company);
}