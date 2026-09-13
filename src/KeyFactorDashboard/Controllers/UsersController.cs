using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = PlantRoles.AdminOnly)]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<PlantUser> _hasher = new();

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(List<UserRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserRowDto>>> List(CancellationToken cancellationToken)
    {
        var rows = await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(cancellationToken);
        return rows.Select(ToRow).ToList();
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserRowDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserRowDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { errorCode = "invalidUser" });
        }

        if (!PlantRoles.All.Contains(request.Role))
        {
            return BadRequest(new { errorCode = "invalidRole" });
        }

        if (await _db.Users.AnyAsync(u => u.Username == request.Username, cancellationToken))
        {
            return Conflict(new { errorCode = "usernameTaken" });
        }

        var user = new PlantUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = request.Username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return ToRow(user);
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToRow(user));
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        if (!PlantRoles.All.Contains(request.Role))
        {
            return BadRequest(new { errorCode = "invalidRole" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.Role = request.Role;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToRow(user));
    }

    private static UserRowDto ToRow(PlantUser u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        DisplayName = u.DisplayName,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
