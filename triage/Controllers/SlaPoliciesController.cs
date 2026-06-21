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
    [Authorize(Roles = "Admin")]      
    [Route("api/[controller]")]       
    public class SlaPoliciesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _me;

        public SlaPoliciesController(AppDbContext db, ICurrentUser me)
        {
            _db = db;
            _me = me;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.SlaPolicies
                .OrderBy(p => p.Name)
                .Select(p => new SlaPolicyResponse(p.Id, p.Name, p.ResponseMins, p.ResolveMins))
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateSlaPolicyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Name is required." });
            if (req.ResponseMins <= 0 || req.ResolveMins <= 0)
                return BadRequest(new { message = "Response and resolve minutes must be greater than zero." });

            var policy = new SlaPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = _me.TenantId,    
                Name = req.Name.Trim(),
                ResponseMins = req.ResponseMins,
                ResolveMins = req.ResolveMins,
            };

            _db.SlaPolicies.Add(policy);
            await _db.SaveChangesAsync();

            return Ok(new SlaPolicyResponse(policy.Id, policy.Name, policy.ResponseMins, policy.ResolveMins));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateSlaPolicyRequest req)
        {
            var p = await _db.SlaPolicies.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.Name)) p.Name = req.Name.Trim();
            if (req.ResponseMins is > 0) p.ResponseMins = req.ResponseMins.Value;
            if (req.ResolveMins is > 0) p.ResolveMins = req.ResolveMins.Value;

            await _db.SaveChangesAsync();
            return Ok(new SlaPolicyResponse(p.Id, p.Name, p.ResponseMins, p.ResolveMins));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var p = await _db.SlaPolicies.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();

            _db.SlaPolicies.Remove(p);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}