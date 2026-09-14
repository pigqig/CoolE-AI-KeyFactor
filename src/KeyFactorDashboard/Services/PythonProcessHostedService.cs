using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".."));
        var pythonDir = Path.Combine(repoRoot, "python");
        var expectedStamp = ComputePythonCodeStamp(Path.Combine(pythonDir, "app"));

        if (await _client.HealthAsync(cancellationToken))
        {
            var remoteStamp = await ReadRemoteCodeStampAsync(cancellationToken);
            if (!string.IsNullOrEmpty(remoteStamp)
                && string.Equals(remoteStamp, expectedStamp, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Python analysis service already running at {Url} (code {Stamp}).",
                    _options.BaseUrl,
                    remoteStamp);
                return;
            }

            _logger.LogInformation(
                "Existing Python worker on {Url} is stale or unstamped (remote={Remote}, local={Local}). Starting a fresh worker.",
                _options.BaseUrl,
                remoteStamp ?? "(none)",
                expectedStamp);
            TryStopListenerOnPort(_options.Port);
            await Task.Delay(400, cancellationToken);
        }

        var sample = Path.Combine(repoRoot, "samples", "process-quality.csv");
        var python = ResolvePython(pythonDir);
        if (python is null)
        {
            _logger.LogWarning("Python 3 was not found. Start uvicorn manually from python/.");
            return;
        }

        var reload = _environment.IsDevelopment() ? " --reload" : "";
        var start = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"-m uvicorn app.main:app --host 127.0.0.1 --port {_options.Port}{reload}",
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

    private async Task<string?> ReadRemoteCodeStampAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var url = _options.BaseUrl.TrimEnd('/') + "/health";
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<HealthStampBody>(
                PythonApiClient.JsonOptions,
                cancellationToken);
            return string.IsNullOrWhiteSpace(body?.CodeStamp) ? null : body.CodeStamp;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to read Python health codeStamp.");
            return null;
        }
    }

    private void TryStopListenerOnPort(int port)
    {
        foreach (var pid in FindPidsListeningOnPort(port))
        {
            if (pid <= 0)
            {
                continue;
            }

            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(3000);
                _logger.LogInformation("Stopped stale Python worker PID {Pid} on port {Port}.", pid, port);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to stop PID {Pid} on port {Port}.", pid, port);
            }
        }
    }

    private static IEnumerable<int> FindPidsListeningOnPort(int port)
    {
        var pids = new HashSet<int>();
        if (OperatingSystem.IsWindows())
        {
            CollectPidsFromCommand(pids, "netstat", "-ano -p tcp", line =>
            {
                if (line.IndexOf($":{port}", StringComparison.Ordinal) < 0
                    || line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[^1], out var pid))
                {
                    pids.Add(pid);
                }
            });
            return pids;
        }

        CollectPidsFromCommand(pids, "ss", $"-lptn sport = :{port}", line =>
        {
            foreach (Match match in Regex.Matches(line, @"pid=(\d+)"))
            {
                if (int.TryParse(match.Groups[1].Value, out var pid))
                {
                    pids.Add(pid);
                }
            }
        });
        if (pids.Count == 0)
        {
            CollectPidsFromCommand(pids, "lsof", $"-t -iTCP:{port} -sTCP:LISTEN", line =>
            {
                if (int.TryParse(line.Trim(), out var pid))
                {
                    pids.Add(pid);
                }
            });
        }

        if (pids.Count == 0)
        {
            CollectPidsFromCommand(pids, "fuser", $"{port}/tcp", line =>
            {
                foreach (Match match in Regex.Matches(line, @"\d+"))
                {
                    if (int.TryParse(match.Value, out var pid))
                    {
                        pids.Add(pid);
                    }
                }
            });
        }

        return pids;
    }

    private static void CollectPidsFromCommand(HashSet<int> pids, string fileName, string args, Action<string> parseLine)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(start);
            if (proc is null)
            {
                return;
            }

            var output = proc.StandardOutput.ReadToEnd() + "\n" + proc.StandardError.ReadToEnd();
            proc.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    parseLine(line);
                }
            }
        }
        catch
        {
            // Command is optional; missing ss/lsof/fuser is fine.
        }
    }

    internal static string ComputePythonCodeStamp(string pythonAppDir)
    {
        if (!Directory.Exists(pythonAppDir))
        {
            return "";
        }

        using var sha = SHA256.Create();
        foreach (var path in Directory.GetFiles(pythonAppDir, "*.py").OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
        {
            var name = Encoding.UTF8.GetBytes(Path.GetFileName(path));
            sha.TransformBlock(name, 0, name.Length, null, 0);
            sha.TransformBlock(new byte[] { 0 }, 0, 1, null, 0);
            var bytes = File.ReadAllBytes(path);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hex = Convert.ToHexString(sha.Hash ?? []).ToLowerInvariant();
        return hex.Length <= 16 ? hex : hex[..16];
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

    private sealed class HealthStampBody
    {
        public string? CodeStamp { get; set; }
    }
}
