namespace KeyFactorDashboard.Options;

public sealed class PythonOptions
{
    public const string SectionName = "Python";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public bool AutoStart { get; set; } = true;
    public int Port { get; set; } = 8001;
}

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>If empty, the API is open. If set, callers must send X-Api-Key.</summary>
    public string? Key { get; set; }

    public bool EnableSwagger { get; set; } = true;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string[] Origins { get; set; } = [];
}
