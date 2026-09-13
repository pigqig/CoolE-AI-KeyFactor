using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = PlantRoles.AdminOnly)]
[Produces("application/json")]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(List<AuditRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditRowDto>>> Latest(CancellationToken cancellationToken)
    {
        var rows = await _db.AuditEvents.AsNoTracking()
            .OrderByDescending(a => a.At)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(a => new AuditRowDto
        {
            Id = a.Id,
            At = a.At,
            UserId = a.UserId,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            DetailJson = a.DetailJson
        }).ToList();
    }
}
