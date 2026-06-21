using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using triage.Data;
using triage.Dtos;
using triage.Entities;
using triage.Services;

namespace triage.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _me;
        private readonly ITicketEventService _events;
        private readonly IRealtimeNotifier _notifier;
        private readonly ICacheService _cache;

        public TicketsController(AppDbContext db, ICurrentUser me,
                                 ITicketEventService events, IRealtimeNotifier notifier,
                                 ICacheService cache)
        {
            _db = db;
            _me = me;
            _events = events;
            _notifier = notifier;
            _cache = cache;
        }

        private string StatsCacheKey => $"stats:tenant:{_me.TenantId}:user:{_me.UserId}:role:{_me.Role}";
        private Task InvalidateStatsAsync() => _cache.RemoveAsync(StatsCacheKey);

        private bool IsRequester => string.Equals(_me.Role, "Requester", StringComparison.OrdinalIgnoreCase);

       
        private IQueryable<TicketResponse> Project(IQueryable<Ticket> q) =>
            q.Select(t => new TicketResponse(
                t.Id,
                t.Subject,
                t.Status,
                t.Priority,
                _db.Users.Where(u => u.Id == t.RequesterId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                t.AssigneeId,
                _db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FullName).FirstOrDefault(),
                t.DueAt,
                t.CreatedAt));

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? priority)
        {
            var query = _db.Tickets.AsQueryable();

            if (IsRequester)
                query = query.Where(t => t.RequesterId == _me.UserId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
            if (!string.IsNullOrWhiteSpace(priority))
                query = query.Where(t => t.Priority == priority);

            var tickets = await Project(query.OrderByDescending(t => t.CreatedAt)).ToListAsync();
            return Ok(tickets);
        }

        
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var cached = await _cache.GetAsync<TicketStatsResponse>(StatsCacheKey);
            if (cached != null) return Ok(cached);

            var thirty = DateTime.UtcNow.AddMinutes(30);
            var query = _db.Tickets.AsQueryable();
            if (IsRequester) query = query.Where(t => t.RequesterId == _me.UserId);

            var stats = new TicketStatsResponse(
                Open: await query.CountAsync(t => t.Status == "Open"),
                Pending: await query.CountAsync(t => t.Status == "Pending"),
                Resolved: await query.CountAsync(t => t.Status == "Resolved"),
                Breaching: await query.CountAsync(t => t.DueAt != null && t.DueAt <= thirty && t.Status != "Resolved"));

            await _cache.SetAsync(StatsCacheKey, stats, TimeSpan.FromSeconds(30));
            return Ok(stats);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var q = _db.Tickets.Where(t => t.Id == id);
            if (IsRequester) q = q.Where(t => t.RequesterId == _me.UserId);

            var ticket = await Project(q).FirstOrDefaultAsync();
            return ticket is null ? NotFound() : Ok(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateTicketRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Subject))
                return BadRequest(new { message = "Subject is required." });

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                TenantId = _me.TenantId,        
                RequesterId = _me.UserId,
                AssigneeId = req.AssigneeId,
                Subject = req.Subject.Trim(),
                Status = "Open",
                Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Medium" : req.Priority,
                CreatedAt = DateTime.UtcNow,
            };

           
            var policy = await _db.SlaPolicies.OrderBy(p => p.ResolveMins).FirstOrDefaultAsync();
            if (policy != null)
                ticket.DueAt = ticket.CreatedAt.AddMinutes(policy.ResolveMins);

          
            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();

            _events.Track(ticket.Id, "Created", "Ticket created");
            await _db.SaveChangesAsync();

            await _notifier.TicketChangedAsync(_me.TenantId, ticket.Id, "created", $"New ticket: {ticket.Subject}");
            await InvalidateStatsAsync();

            var created = await Project(_db.Tickets.Where(t => t.Id == ticket.Id)).FirstAsync();
            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, created);
        }

        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateTicketRequest req)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null) return NotFound();

            var changes = new List<string>();

            if (!string.IsNullOrWhiteSpace(req.Status) && req.Status != ticket.Status)
            {
                _events.Track(id, "StatusChanged", $"Status changed from {ticket.Status} to {req.Status}");
                changes.Add($"status → {req.Status}");
                ticket.Status = req.Status;
            }

            if (!string.IsNullOrWhiteSpace(req.Priority) && req.Priority != ticket.Priority)
            {
                _events.Track(id, "PriorityChanged", $"Priority changed from {ticket.Priority} to {req.Priority}");
                changes.Add($"priority → {req.Priority}");
                ticket.Priority = req.Priority;
            }

            if (req.AssigneeId.HasValue && req.AssigneeId != ticket.AssigneeId)
            {
                var name = await _db.Users
                    .Where(u => u.Id == req.AssigneeId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync();
                _events.Track(id, "Reassigned", name != null ? $"Assigned to {name}" : "Reassigned");
                changes.Add(name != null ? $"assigned to {name}" : "reassigned");
                ticket.AssigneeId = req.AssigneeId;
            }

            await _db.SaveChangesAsync();

            var message = changes.Count > 0
                ? $"\"{ticket.Subject}\" — {string.Join(", ", changes)}"
                : $"\"{ticket.Subject}\" updated";
            await _notifier.TicketChangedAsync(_me.TenantId, id, "updated", message);
            await InvalidateStatsAsync();

            var updated = await Project(_db.Tickets.Where(t => t.Id == id)).FirstAsync();
            return Ok(updated);
        }

        [HttpGet("{id:guid}/comments")]
        public async Task<IActionResult> GetComments(Guid id)
        {
            if (!await _db.Tickets.AnyAsync(t => t.Id == id)) return NotFound();

            var isRequester = string.Equals(_me.Role, "Requester", StringComparison.OrdinalIgnoreCase);

            var query = _db.TicketComments.Where(c => c.TicketId == id);

            if (isRequester)
                query = query.Where(c => !c.IsInternal);

            var comments = await query
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentResponse(
                    c.Id,
                    c.Body,
                    c.IsInternal,
                    _db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                    c.CreatedAt))
                .ToListAsync();

            return Ok(comments);
        }

        [HttpPost("{id:guid}/comments")]
        public async Task<IActionResult> AddComment(Guid id, CreateCommentRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Body))
                return BadRequest(new { message = "Comment body is required." });

            if (!await _db.Tickets.AnyAsync(t => t.Id == id)) return NotFound();

           
            var isRequester = string.Equals(_me.Role, "Requester", StringComparison.OrdinalIgnoreCase);
            var isInternal = !isRequester && req.IsInternal;

            var comment = new TicketComment
            {
                Id = Guid.NewGuid(),
                TenantId = _me.TenantId,    
                TicketId = id,
                AuthorId = _me.UserId,
                Body = req.Body.Trim(),
                IsInternal = isInternal,
                CreatedAt = DateTime.UtcNow,
            };

            _db.TicketComments.Add(comment);
            await _db.SaveChangesAsync();

            await _notifier.TicketChangedAsync(_me.TenantId, id, "commented", "New comment added");

            var created = await _db.TicketComments
                .Where(c => c.Id == comment.Id)
                .Select(c => new CommentResponse(
                    c.Id,
                    c.Body,
                    c.IsInternal,
                    _db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                    c.CreatedAt))
                .FirstAsync();

            return Ok(created);
        }

        [HttpGet("{id:guid}/events")]
        public async Task<IActionResult> GetEvents(Guid id)
        {
            if (!await _db.Tickets.AnyAsync(t => t.Id == id)) return NotFound();

            var events = await _db.TicketEvents
                .Where(e => e.TicketId == id)
                .OrderBy(e => e.CreatedAt)
                .Select(e => new TicketEventResponse(
                    e.Id,
                    e.EventType,
                    e.Detail,
                    e.ActorId == null
                        ? "System"
                        : _db.Users.Where(u => u.Id == e.ActorId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                    e.CreatedAt))
                .ToListAsync();

            return Ok(events);
        }
    }
}