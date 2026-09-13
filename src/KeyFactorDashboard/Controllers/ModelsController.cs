using System.Text;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyFactorDashboard.Controllers;

/// <summary>Train and explain. Metrics / importances are cached on the C# side so a restart still has history.</summary>
[ApiController]
[Route("api/v1/models")]
[Authorize]
[Produces("application/json")]
public sealed class ModelsController : ApiControllerBase
{
    private readonly IPythonApiClient _python;
    private readonly AnalysisLedger _ledger;
    private readonly AuditSink _audit;

    public ModelsController(IPythonApiClient python, AnalysisLedger ledger, AuditSink audit)
    {
        _python = python;
        _ledger = ledger;
        _audit = audit;
    }

    /// <summary>Fit gradient boosting or random forest. Two unique target values → classification.</summary>
    [HttpPost("train")]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModelResponse>> Train([FromBody] TrainRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DatasetId) || string.IsNullOrWhiteSpace(request.TargetColumn))
        {
            return Problem(detail: "datasetId and targetColumn are required.", statusCode: 400, title: "missingTarget");
        }

        try
        {
            await EnsurePythonHasDataset(request.DatasetId, cancellationToken);
            var model = await _python.TrainAsync(request, cancellationToken);
            var artifact = Decode(model.ArtifactBase64);
            model.ArtifactBase64 = null;
            ImportancesResponse? imp = null;
            try
            {
                imp = await _python.GetImportancesAsync(model.Id, null, cancellationToken);
            }
            catch (PythonApiException)
            {
                // train still counts; factor tab can retry
            }

            await _ledger.RememberJobAsync(model, imp, artifact, User.UserId(), cancellationToken);
            var job = await _ledger.GetJobAsync(model.Id, cancellationToken);
            if (job is not null)
            {
                model = await _ledger.ToModelDtoAsync(job, cancellationToken);
            }

            await _audit.WriteAsync("Train", "ModelVersion", model.Id, new { model.DatasetId, model.VersionNumber }, cancellationToken);
            return Ok(model);
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Metrics plus the last stored overview.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModelResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var job = await _ledger.GetJobAsync(id, cancellationToken);
        if (job is not null)
        {
            return Ok(await _ledger.ToModelDtoAsync(job, cancellationToken));
        }

        try
        {
            return Ok(await _python.GetModelAsync(id, cancellationToken));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Permutation ranks and insight cards. Uses the SQLite cache after a restart.</summary>
    [HttpGet("{id}/importances")]
    [ProducesResponseType(typeof(ImportancesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportancesResponse>> Importances(
        string id,
        [FromQuery] int? topN,
        CancellationToken cancellationToken)
    {
        var cached = await _ledger.CachedImportancesAsync(id, cancellationToken);
        if (cached is not null && topN is null or 0)
        {
            return Ok(cached);
        }

        try
        {
            await WakeModel(id, cancellationToken);
            var live = await _python.GetImportancesAsync(id, topN, cancellationToken);
            var job = await _ledger.GetJobAsync(id, cancellationToken);
            if (job is not null)
            {
                await _ledger.RememberJobAsync(_ledger.ToModelDto(job), live, null, User.UserId(), cancellationToken);
            }

            return Ok(live);
        }
        catch (PythonApiException ex)
        {
            if (cached is not null)
            {
                return Ok(cached);
            }

            return FromPython(ex);
        }
    }

    /// <summary>Partial-dependence curve plus a caption a line tech can read.</summary>
    [HttpGet("{id}/dependence")]
    [ProducesResponseType(typeof(DependenceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DependenceResponse>> Dependence(
        string id,
        [FromQuery] string feature,
        [FromQuery] string? colorBy,
        [FromQuery] string? @class,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(feature))
        {
            return Problem(detail: "feature is required.", statusCode: 400, title: "unknownFeature");
        }

        try
        {
            await WakeModel(id, cancellationToken);
            return Ok(await _python.GetDependenceAsync(id, feature, colorBy, cancellationToken, @class));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Clone a row, apply edits, predict. Body: <c>rowIndex</c> or <c>row</c>, plus <c>edits</c>.</summary>
    [HttpPost("{id}/whatif")]
    [Authorize(Policy = PlantRoles.CanMutate)]
    [ProducesResponseType(typeof(WhatIfResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WhatIfResponse>> WhatIf(
        string id,
        [FromBody] WhatIfRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await WakeModel(id, cancellationToken);
            var result = await _python.WhatIfAsync(id, request, cancellationToken);
            await _ledger.RememberWhatIfAsync(id, request, result, cancellationToken);
            await _audit.WriteAsync("WhatIf", "ModelVersion", id, new { request.RowIndex }, cancellationToken);
            return Ok(result);
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    /// <summary>Scored rows for the individual-prediction table.</summary>
    [HttpGet("{id}/rows")]
    [ProducesResponseType(typeof(RowsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RowsResponse>> Rows(string id, CancellationToken cancellationToken)
    {
        try
        {
            await WakeModel(id, cancellationToken);
            return Ok(await _python.GetRowsAsync(id, cancellationToken));
        }
        catch (PythonApiException ex)
        {
            return FromPython(ex);
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Policy = PlantRoles.CanApprove)]
    public async Task<IActionResult> Approve(string id, [FromBody] VersionNoteRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            await _ledger.ApproveAsync(id, User.UserId() ?? "", request?.Note, cancellationToken);
            await _audit.WriteAsync("Approve", "ModelVersion", id, new { request?.Note }, cancellationToken);
            var job = await _ledger.GetJobAsync(id, cancellationToken);
            return job is null ? Ok(new { id, status = ModelStatuses.Approved }) : Ok(await _ledger.ToModelDtoAsync(job, cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { errorCode = "versionNotFound" });
        }
    }

    [HttpPost("{id}/reject")]
    [Authorize(Policy = PlantRoles.CanApprove)]
    public async Task<IActionResult> Reject(string id, [FromBody] VersionNoteRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            await _ledger.RejectAsync(id, User.UserId() ?? "", request?.Note, cancellationToken);
            await _audit.WriteAsync("Reject", "ModelVersion", id, new { request?.Note }, cancellationToken);
            var job = await _ledger.GetJobAsync(id, cancellationToken);
            return job is null ? Ok(new { id, status = ModelStatuses.Rejected }) : Ok(await _ledger.ToModelDtoAsync(job, cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { errorCode = "versionNotFound" });
        }
    }

    private async Task EnsurePythonHasDataset(string datasetId, CancellationToken ct)
    {
        try
        {
            await _python.GetDatasetAsync(datasetId, null, ct);
            return;
        }
        catch (PythonApiException ex) when (ex.StatusCode == 404)
        {
            // RAM expired after uvicorn recycle — push the blob back in
        }

        var row = await _ledger.GetDatasetRowAsync(datasetId, ct);
        if (row is null)
        {
            throw new PythonApiException(404, "datasetNotFound", "Dataset was not found.");
        }

        DatasetResponse parsed;
        if (row.PayloadKind == "json")
        {
            var json = Encoding.UTF8.GetString(row.Payload);
            var body = System.Text.Json.JsonSerializer.Deserialize<JsonDatasetRequest>(json, PythonApiClient.JsonOptions)
                       ?? new JsonDatasetRequest();
            body.Id = datasetId;
            parsed = await _python.UploadJsonAsync(body, ct);
        }
        else
        {
            await using var stream = new MemoryStream(row.Payload);
            parsed = await _python.UploadCsvAsync(stream, row.Name, row.Name, ct, datasetId);
        }

        if (parsed.Id != datasetId && row.PayloadKind == "json")
        {
            // older python without id reuse — last resort
            _ = parsed;
        }
    }

    private async Task WakeModel(string modelId, CancellationToken ct)
    {
        try
        {
            await _python.GetModelAsync(modelId, ct);
            return;
        }
        catch (PythonApiException ex) when (ex.StatusCode is 404)
        {
        }

        var job = await _ledger.GetJobAsync(modelId, ct);
        if (job?.Artifact is { Length: > 0 })
        {
            await _python.RestoreAsync(Convert.ToBase64String(job.Artifact), modelId, ct);
        }
    }

    private static byte[]? Decode(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
