using System.Security.Claims;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Options;
using Microsoft.Extensions.Options;

namespace KeyFactorDashboard.Middleware;

public sealed class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly ApiOptions _options;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresKey(context.Request.Path) || string.IsNullOrWhiteSpace(_options.Key))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(HeaderName, out var provided))
        {
            if (!string.Equals(provided.ToString(), _options.Key, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/401",
                    title = "Unauthorized",
                    status = 401,
                    detail = "Missing or invalid X-Api-Key.",
                    errorCode = "unauthorized"
                });
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                var id = new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "machine"),
                        new Claim(ClaimTypes.Name, "apikey"),
                        new Claim(ClaimTypes.Role, PlantRoles.Engineer),
                        new Claim("displayName", "API Key")
                    ],
                    "ApiKey");
                context.User = new ClaimsPrincipal(id);
            }
        }

        await _next(context);
    }

    private static bool RequiresKey(PathString path)
    {
        var value = path.Value ?? "";
        return value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
               && !value.StartsWith("/api/v1/health", StringComparison.OrdinalIgnoreCase)
               && !value.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);
    }
}
