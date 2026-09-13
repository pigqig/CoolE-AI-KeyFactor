namespace KeyFactorDashboard.Data;

internal static class SqlitePath
{
    public static string Resolve(string connectionString, string contentRoot)
    {
        const string key = "Data Source=";
        var idx = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return connectionString;
        }

        var file = connectionString[(idx + key.Length)..].Trim();
        if (file is ":memory:" or "file::memory:")
        {
            return connectionString;
        }

        if (!Path.IsPathRooted(file))
        {
            file = Path.GetFullPath(Path.Combine(contentRoot, file));
        }

        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return string.Concat(connectionString.AsSpan(0, idx + key.Length), file);
    }
}
