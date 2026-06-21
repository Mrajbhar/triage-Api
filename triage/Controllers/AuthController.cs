using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using triage.Data;
using triage.Dtos;
using triage.Entities;
using triage.Services;

namespace triage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]  
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokens;
        private readonly IPasswordHasher<User> _hasher;
        private readonly ICurrentUser _me;

        public AuthController(AppDbContext db, ITokenService tokens, IPasswordHasher<User> hasher, ICurrentUser me)
        {
            _db = db;
            _tokens = tokens;
            _hasher = hasher;
            _me = me;
        }

       
        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest req)
        {
            var email = req.Email.Trim().ToLowerInvariant();

            if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email))
                return Conflict(new { message = "An account with this email already exists." });

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = req.CompanyName,
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = email,
                FullName = req.FullName,
                Role = "Admin",  
            };

            user.PasswordHash = _hasher.HashPassword(user, req.Password);

            _db.Tenants.Add(tenant);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = _tokens.CreateToken(user, tenant.Name);
            return Ok(new AuthResponse(token, user.Id, user.FullName, user.Email, user.Role, user.TenantId));
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req)
        {
            var email = req.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);

          
            if (user is null)
                return Unauthorized(new { message = "Invalid email or password." });

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid email or password." });

            var company = await _db.Tenants.IgnoreQueryFilters()
                .Where(t => t.Id == user.TenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync() ?? "";

            var token = _tokens.CreateToken(user, company);
            return Ok(new AuthResponse(token, user.Id, user.FullName, user.Email, user.Role, user.TenantId));
        }

       
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _me.UserId);
            if (user is null) return NotFound();

            var company = await _db.Tenants
                .Where(t => t.Id == user.TenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync() ?? "";

            return Ok(new MeResponse(user.Id, user.FullName, user.Email, user.Role, user.TenantId, company));
        }

      
        [Authorize]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserListItem(u.Id, u.FullName, u.Email, u.Role))
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser(CreateUserRequest req)
        {
            var allowed = new[] { "Admin", "Agent", "Requester" };
            var role = allowed.FirstOrDefault(r => string.Equals(r, req.Role?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (role is null)
                return BadRequest(new { message = "Role must be Admin, Agent, or Requester." });
            if (string.IsNullOrWhiteSpace(req.FullName))
                return BadRequest(new { message = "Name is required." });
            if ((req.Password ?? "").Length < 6)
                return BadRequest(new { message = "Password must be at least 6 characters." });

            var email = req.Email.Trim().ToLowerInvariant();

          
            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "A user with this email already exists." });

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = _me.TenantId,         
                Email = email,
                FullName = req.FullName.Trim(),
                Role = role,
                CreatedAt = DateTime.UtcNow,
            };
            user.PasswordHash = _hasher.HashPassword(user, req.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new UserListItem(user.Id, user.FullName, user.Email, user.Role));
        }
    }
}