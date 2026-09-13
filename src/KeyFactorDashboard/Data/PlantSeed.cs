using KeyFactorDashboard.Auth;
using Microsoft.AspNetCore.Identity;

namespace KeyFactorDashboard.Data;

public static class PlantSeed
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "ChangeMe!123";

    public static void Ensure(AppDbContext db)
    {
        foreach (var name in PlantRoles.All)
        {
            if (!db.Roles.Any(r => r.Name == name))
            {
                db.Roles.Add(new RoleRecord { Name = name });
            }
        }

        if (!db.Users.Any(u => u.Username == AdminUser))
        {
            var hasher = new PasswordHasher<PlantUser>();
            var admin = new PlantUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Username = AdminUser,
                DisplayName = "廠內管理員",
                Role = PlantRoles.Admin,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, AdminPassword);
            db.Users.Add(admin);
        }

        db.SaveChanges();
    }
}
