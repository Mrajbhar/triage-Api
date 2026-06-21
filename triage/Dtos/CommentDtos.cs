using System.ComponentModel.DataAnnotations;

namespace triage.Dtos
{
   
    public record CreateCommentRequest(
        [Required, StringLength(5000, MinimumLength = 1)] string Body,
        bool IsInternal);

    public record CommentResponse(
        Guid Id,
        string Body,
        bool IsInternal,
        string Author,
        DateTime CreatedAt);
}