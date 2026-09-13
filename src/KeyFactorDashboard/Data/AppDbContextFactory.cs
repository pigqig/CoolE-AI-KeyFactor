using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KeyFactorDashboard.Data;

/// <summary>Used by <c>dotnet ef migrations add</c>. Defaults to the shop-floor SQLite file.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var sqlServer = args.Any(a => a.Contains("SqlServer", StringComparison.OrdinalIgnoreCase));
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
        {
            builder.UseSqlServer("Server=localhost;Database=KeyFactor;Trusted_Connection=True;TrustServerCertificate=True");
        }
        else
        {
            builder.UseSqlite("Data Source=App_Data/keyfactor.db");
        }

        return new AppDbContext(builder.Options);
    }
}
