using System.Diagnostics;
using KeyFactorDashboard.Options;
using Microsoft.Extensions.Options;

namespace KeyFactorDashboard.Services;

public sealed class PythonProcessHostedService : IHostedService, IDisposable
{
    private readonly ILogger<PythonProcessHostedService> _logger;
    private readonly PythonOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IPythonApiClient _client;
    private Process? _process;

    public PythonProcessHostedService(
        ILogger<PythonProcessHostedService> logger,
        IOptions<PythonOptions> options,
        IWebHostEnvironment environment,
        IPythonApiClient client)
    {
        _logger = logger;
        _options = options.Value;
        _environment = environment;
        _client = client;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoStart)
        {
            _logger.LogInformation("Python auto-start is disabled.");
            return;
        }

        if (await _client.HealthAsync(cancellationToken))
        {
            _logger.LogInformation("Python analysis service already running at {Url}.", _options.BaseUrl);
            return;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".."));
        var pythonDir = Path.Combine(repoRoot, "python");
        var sample = Path.Combine(repoRoot, "samples", "process-quality.csv");
        var python = ResolvePython(pythonDir);
        if (python is null)
        {
            _logger.LogWarning("Python 3 was not found. Start uvicorn manually from python/.");
            return;
        }

        var start = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"-m uvicorn app.main:app --host 127.0.0.1 --port {_options.Port}",
            WorkingDirectory = pythonDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["KEYFACTOR_SAMPLE_CSV"] = sample;

        try
        {
            _process = Process.Start(start);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start the Python analysis service.");
            return;
        }

        if (_process is null)
        {
            return;
        }

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogInformation("python: {Line}", e.Data);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogInformation("python: {Line}", e.Data);
            }
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        for (var i = 0; i < 40 && !cancellationToken.IsCancellationRequested; i++)
        {
            if (await _client.HealthAsync(cancellationToken))
            {
                _logger.LogInformation("Python analysis service is ready.");
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        _logger.LogWarning("Python analysis service did not become ready. APIs that need sklearn will return 503.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        TryStop();
        return Task.CompletedTask;
    }

    public void Dispose() => TryStop();

    private void TryStop()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to stop Python process.");
            }
        }

        _process?.Dispose();
        _process = null;
    }

    private static string? ResolvePython(string pythonDir)
    {
        var candidates = new[]
        {
            Path.Combine(pythonDir, ".venv", "bin", "python"),
            Path.Combine(pythonDir, ".venv", "Scripts", "python.exe"),
            "python3",
            "python"
        };

        foreach (var candidate in candidates)
        {
            if (candidate is "python3" or "python")
            {
                return candidate;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
