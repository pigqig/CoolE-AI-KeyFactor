namespace KeyFactorDashboard.Auth;

public static class PlantRoles
{
    public const string Admin = "Admin";
    public const string Engineer = "Engineer";
    public const string Qa = "Qa";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Engineer, Qa, Viewer];

    public const string CanMutate = "CanMutate";
    public const string CanApprove = "CanApprove";
    public const string AdminOnly = "AdminOnly";
}

public static class UserStamp
{
    public static string? UserId(this System.Security.Claims.ClaimsPrincipal user)
        => user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public static string? RoleName(this System.Security.Claims.ClaimsPrincipal user)
        => user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
}
