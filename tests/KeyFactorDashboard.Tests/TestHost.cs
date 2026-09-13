using KeyFactorDashboard.Data;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KeyFactorDashboard.Tests;

public sealed class TestHost : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var db = Path.Combine(Path.GetTempPath(), $"kf-test-{Guid.NewGuid():N}.db");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Python:AutoStart"] = "false",
                ["Python:BaseUrl"] = "http://127.0.0.1:9",
                ["Api:Key"] = "",
                ["Api:EnableSwagger"] = "true",
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Default"] = $"Data Source={db}",
                ["Auth:SigningKey"] = "plant-edition-dev-signing-key-32b!",
                ["Auth:Issuer"] = "KeyFactor",
                ["Auth:Audience"] = "KeyFactor"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPythonApiClient>();
            services.AddSingleton<IPythonApiClient, FakePythonApiClient>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={db}"));
        });
    }
}
