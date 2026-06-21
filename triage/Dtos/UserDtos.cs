using System.ComponentModel.DataAnnotations;

namespace triage.Dtos
{
   
    public record UserListItem(Guid Id, string FullName, string Email, string Role);

   
    public record CreateUserRequest(
        [Required, StringLength(150, MinimumLength = 1)] string FullName,
        [Required, EmailAddress, StringLength(256)] string Email,
        [Required] string Role,
        [Required, MinLength(6), StringLength(100)] string Password);
}