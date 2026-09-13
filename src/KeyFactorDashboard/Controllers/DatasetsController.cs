using System.Text;
using System.Text.Json;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyFactorDashboard.Controllers;

/// <summary>Ingest process-parameter tables from the Vue UI or an external system. Rows live in SQLite/SQL Server.</summary>
[ApiController]
[Route("api/v1/datasets")]
[Authorize]
[Produces("application/json")]
public sealed class DatasetsController : ApiControllerBase
{
    private readonly IPythonApiClient _python;
    private readonly AnalysisLedger _ledger;
    private readonly IWebHostEnvironment _env;
    private readonly AuditSink _audit;

    public DatasetsController(IPythonApiClient python, AnalysisLedger ledger, IWebHostEnvironment env, AuditSink audit)
    {
        _python = python;
        _ledger = ledger;
        _env = env;
        _audit = audit;
    }

    /// <summary>Datasets still on disk after a restart.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DatasetResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DatasetResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _ledger.ListDatasetsAsync(cancellationToken));

    /// <summary>
    /// Create a dataset from a multipart CSV upload or a JSON table.
    /// JSON body: <c>{ "name": "line-A", "columns": ["Temperature","TargetConductivity"], "rows": [[180,12.4]] }</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [Consumes("multipart/form-data", "application/json")]
    [ProducesResponseType(typeof(DatasetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DatasetResponse>> Create(CancellationToken cancellationToken)
    {
        try
        {
            if (Request.HasFormContentType)
            {
                var file = Request.Form.Files["file"] ?? Request.Form.Files.FirstOrDefault();
                if (file is null || file.Length == 0)
                {
                    return Problem(detail: "CSV file is required.", statusCode: 400, title: "emptyDataset");
                }

                await using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                var bytes = buffer.ToArray();
                buffer.Position = 0;
                var name = Request.Form.TryGetValue("name", out var given) ? given.ToString() : file.FileName;
                var parsed = await _python.UploadCsvAsync(buffer, file.FileName, name, cancellationToken);
                var saved = await _ledger.RememberDatasetAsync(parsed, bytes, "csv", cancellationToken);
                await _audit.WriteAsync("Ingest", "Dataset", saved.Id, new { saved.Name, saved.RowCount }, cancellationToken);
                return Ok(saved);
            }

            var json = await Request.ReadFromJsonAsync<JsonDatasetRequest>(PythonApiClient.JsonOptions, cancellationToken);
            if (json is null)
            {
                return Problem(detail: "JSON dataset body is required.", statusCode: 400, title: "invalidRows");
            }

            return Ok(await IngestJson(json, cancellationToken));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>JSON ingest (same as POST /api/v1/datasets) for Swagger Try it out.</summary>
    [HttpPost("json")]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(DatasetResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DatasetResponse>> CreateJson([FromBody] JsonDatasetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await IngestJson(request, cancellationToken));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Load a built-in reman sample. <c>kind=okng</c> loads OK/NG; default is conductivity.</summary>
    [HttpPost("sample")]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [ProducesResponseType(typeof(DatasetResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DatasetResponse>> LoadSample(
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        if (string.Equals(kind, "okng", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadSampleOkng(cancellationToken);
        }

        try
        {
            var parsed = await _python.LoadSampleAsync(cancellationToken);
            var bytes = await ReadSampleBytes("quality");
            var saved = await _ledger.RememberDatasetAsync(parsed, bytes, "csv", cancellationToken);
            await _audit.WriteAsync("Ingest", "Dataset", saved.Id, new { saved.Name, kind = "sample" }, cancellationToken);
            return Ok(saved);
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Load the OK/NG reman sample (process settings → QualityResult).</summary>
    [HttpPost("sample-okng")]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [ProducesResponseType(typeof(DatasetResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DatasetResponse>> LoadSampleOkng(CancellationToken cancellationToken)
    {
        try
        {
            var parsed = await _python.LoadSampleOkngAsync(cancellationToken);
            var bytes = await ReadSampleBytes("okng");
            var saved = await _ledger.RememberDatasetAsync(parsed, bytes, "csv", cancellationToken);
            await _audit.WriteAsync("Ingest", "Dataset", saved.Id, new { saved.Name, kind = "okng" }, cancellationToken);
            return Ok(saved);
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Schema, missing-value notes, preview, overview. Served from the database after ingest.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DatasetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatasetResponse>> Get(string id, [FromQuery] string? target, CancellationToken cancellationToken)
    {
        var stored = await _ledger.GetDatasetAsync(id, cancellationToken);
        if (stored is not null)
        {
            if (!string.IsNullOrWhiteSpace(target) && stored.Overview is not null)
            {
                stored.Overview.TargetColumn = target;
                stored.Overview.InsightCode = "datasetOverviewWithTarget";
            }

            return Ok(stored);
        }

        try
        {
            return Ok(await _python.GetDatasetAsync(id, target, cancellationToken));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Draft / Approved / Rejected / Retired versions for this dataset.</summary>
    [HttpGet("{id}/versions")]
    [ProducesResponseType(typeof(List<ModelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ModelResponse>>> Versions(string id, CancellationToken cancellationToken)
        => Ok(await _ledger.ListVersionsAsync(id, cancellationToken));

    private async Task<DatasetResponse> IngestJson(JsonDatasetRequest request, CancellationToken ct)
    {
        var parsed = await _python.UploadJsonAsync(request, ct);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, PythonApiClient.JsonOptions));
        var saved = await _ledger.RememberDatasetAsync(parsed, bytes, "json", ct);
        await _audit.WriteAsync("Ingest", "Dataset", saved.Id, new { saved.Name, saved.RowCount }, ct);
        return saved;
    }

    private async Task<byte[]> ReadSampleBytes(string kind)
    {
        var repo = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", ".."));
        var file = string.Equals(kind, "okng", StringComparison.OrdinalIgnoreCase)
            ? "process-okng.csv"
            : "process-quality.csv";
        var path = Path.Combine(repo, "samples", file);
        if (System.IO.File.Exists(path))
        {
            return await System.IO.File.ReadAllBytesAsync(path);
        }

        return string.Equals(kind, "okng", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetBytes("Temperature,QualityResult\n180,OK\n")
            : Encoding.UTF8.GetBytes("Temperature,TargetConductivity\n180,12.4\n");
    }
}
