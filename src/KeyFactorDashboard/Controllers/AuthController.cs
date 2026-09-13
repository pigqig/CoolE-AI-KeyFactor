using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenIssuer _tokens;
    private readonly AuditSink _audit;
    private readonly PasswordHasher<PlantUser> _hasher = new();

    public AuthController(AppDbContext db, TokenIssuer tokens, AuditSink audit)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
    }

    /// <summary>廠內登入。成功後把 JWT 放在 Authorization: Bearer。</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(Problem("badCredentials"));
        }

        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");
        if (check == PasswordVerificationResult.Failed)
        {
            return Unauthorized(Problem("badCredentials"));
        }

        var (token, expires) = _tokens.Issue(user);
        await _audit.WriteAsync("Login", "User", user.Id, new { user.Username }, cancellationToken, user.Id);
        return new LoginResponse
        {
            Token = token,
            ExpiresAt = expires,
            User = ToMe(user)
        };
    }

    /// <summary>JWT 登出由前端丟 token；此端點只當回條。</summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => Ok(new { ok = true });

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var id = User.UserId();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return ToMe(user);
    }

    private static MeResponse ToMe(PlantUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Role = user.Role
    };

    private static object Problem(string code) => new { errorCode = code, title = code, detail = "Login failed." };
}
