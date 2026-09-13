using FluentAssertions;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Dataset_survives_a_new_context()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kf-ledger-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

        var parsed = new DatasetResponse
        {
            Id = "ds-line-a",
            Name = "line-A.csv",
            RowCount = 12,
            Columns = [new ColumnDto { Name = "Temperature", Type = "numeric" }],
            Overview = new DatasetOverviewDto { RowCount = 12, ColumnCount = 1, Summary = "12 process records." }
        };

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var ledger = new AnalysisLedger(db);
            await ledger.RememberDatasetAsync(parsed, "a,b\n1,2"u8.ToArray(), "csv", CancellationToken.None);
        }

        await using (var db = new AppDbContext(options))
        {
            var ledger = new AnalysisLedger(db);
            var again = await ledger.GetDatasetAsync("ds-line-a", CancellationToken.None);
            again.Should().NotBeNull();
            again!.Name.Should().Be("line-A.csv");
            again.RowCount.Should().Be(12);
            again.Overview!.Summary.Should().NotBeNullOrWhiteSpace();
        }

        File.Delete(path);
    }

    [Fact]
    public async Task Users_and_model_versions_survive_a_new_context()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kf-plant-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

        string adminId;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            PlantSeed.Ensure(db);
            adminId = db.Users.Single(u => u.Username == PlantSeed.AdminUser).Id;

            var ledger = new AnalysisLedger(db);
            await ledger.RememberDatasetAsync(
                new DatasetResponse
                {
                    Id = "ds-plant",
                    Name = "plant.csv",
                    RowCount = 8,
                    Columns = [new ColumnDto { Name = "Temperature", Type = "numeric" }]
                },
                "a,b\n1,2"u8.ToArray(),
                "csv",
                CancellationToken.None);

            await ledger.RememberJobAsync(
                new ModelResponse
                {
                    Id = "m-plant",
                    DatasetId = "ds-plant",
                    TargetColumn = "TargetConductivity",
                    Algorithm = "gbr",
                    Task = "regression",
                    Metrics = new ModelMetricsDto { RSquared = 0.7 }
                },
                null,
                null,
                adminId,
                CancellationToken.None);
        }

        await using (var db = new AppDbContext(options))
        {
            db.Users.Should().Contain(u => u.Username == PlantSeed.AdminUser && u.Role == "Admin");
            db.Users.Single(u => u.Username == PlantSeed.AdminUser).PasswordHash.Should().NotContain(PlantSeed.AdminPassword);
            var ver = db.ModelVersions.Single(v => v.Id == "m-plant");
            ver.Status.Should().Be(ModelStatuses.Draft);
            ver.VersionNumber.Should().Be(1);
            ver.TrainedByUserId.Should().Be(adminId);
        }

        File.Delete(path);
    }
}
