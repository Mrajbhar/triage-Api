using System.ComponentModel.DataAnnotations;

namespace triage.Dtos
{
    public record SlaPolicyResponse(Guid Id, string Name, int ResponseMins, int ResolveMins);

    public record CreateSlaPolicyRequest(
        [property: Required, StringLength(100, MinimumLength = 1)] string Name,
        [property: Range(1, int.MaxValue)] int ResponseMins,
        [property: Range(1, int.MaxValue)] int ResolveMins);

    public record UpdateSlaPolicyRequest(string? Name, int? ResponseMins, int? ResolveMins);
}