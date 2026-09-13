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
        // SQLite 不能在 ORDER BY 使用 DateTimeOffset，先載入再於記憶體排序。
        var rows = await _db.AuditEvents.AsNoTracking().ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(a => a.At)
            .Take(200)
            .Select(a => new AuditRowDto
            {
                Id = a.Id,
                At = a.At,
                UserId = a.UserId,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                DetailJson = a.DetailJson
            })
            .ToList();
    }
}
