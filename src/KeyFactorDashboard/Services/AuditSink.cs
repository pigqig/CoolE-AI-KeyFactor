using System.Text.Json;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;

namespace KeyFactorDashboard.Services;

public sealed class AuditSink
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditSink(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task WriteAsync(string action, string? entityType, string? entityId, object? detail, CancellationToken ct, string? userId = null)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            UserId = userId ?? _http.HttpContext?.User.UserId(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailJson = detail is null ? null : JsonSerializer.Serialize(detail)
        });
        await _db.SaveChangesAsync(ct);
    }
}
