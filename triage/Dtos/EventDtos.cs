namespace triage.Dtos
{
    public record TicketEventResponse(
        Guid Id,
        string EventType,
        string? Detail,
        string Actor,
        DateTime CreatedAt);
}