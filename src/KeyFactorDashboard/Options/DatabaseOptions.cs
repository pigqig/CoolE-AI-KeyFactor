namespace KeyFactorDashboard.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Sqlite (default) or SqlServer. Same AppDbContext either way.</summary>
    public string Provider { get; set; } = "Sqlite";
}
