using System.Text;
using System.Text.Json;
using KeyFactorDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Data;

public sealed class AnalysisLedger
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;

    public AnalysisLedger(AppDbContext db) => _db = db;

    public async Task<DatasetResponse> RememberDatasetAsync(
        DatasetResponse parsed,
        byte[] payload,
        string kind,
        CancellationToken ct)
    {
        var row = await _db.Datasets.FindAsync([parsed.Id], ct);
        if (row is null)
        {
            row = new ProcessDataset { Id = parsed.Id };
            _db.Datasets.Add(row);
        }

        row.Name = parsed.Name;
        row.CreatedAt = DateTimeOffset.UtcNow;
        row.RowCount = parsed.RowCount;
        row.ColumnsJson = JsonSerializer.Serialize(parsed.Columns, Json);
        row.PreviewJson = JsonSerializer.Serialize(parsed.PreviewRows, Json);
        row.OverviewJson = parsed.Overview is null ? null : JsonSerializer.Serialize(parsed.Overview, Json);
        row.PayloadKind = kind;
        row.Payload = payload;
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<List<DatasetResponse>> ListDatasetsAsync(CancellationToken ct)
    {
        var rows = await _db.Datasets.AsNoTracking().OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DatasetResponse?> GetDatasetAsync(string id, CancellationToken ct)
    {
        var row = await _db.Datasets.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<ProcessDataset?> GetDatasetRowAsync(string id, CancellationToken ct)
        => await _db.Datasets.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<TrainJob> RememberJobAsync(
        ModelResponse model,
        ImportancesResponse? importances,
        byte[]? artifact,
        string? trainedByUserId,
        CancellationToken ct)
    {
        var row = await _db.TrainJobs.FindAsync([model.Id], ct);
        if (row is null)
        {
            row = new TrainJob { Id = model.Id };
            _db.TrainJobs.Add(row);
        }

        var createdJob = row.CreatedAt == default;
        row.DatasetId = model.DatasetId ?? "";
        row.TargetColumn = model.TargetColumn;
        row.Algorithm = model.Algorithm;
        row.Task = model.Task;
        if (createdJob)
        {
            row.CreatedAt = DateTimeOffset.UtcNow;
        }

        row.MetricsJson = JsonSerializer.Serialize(model.Metrics, Json);
        row.FeatureNamesJson = JsonSerializer.Serialize(model.FeatureNames, Json);
        row.ImportancesJson = importances is null ? row.ImportancesJson : JsonSerializer.Serialize(importances, Json);
        if (artifact is { Length: > 0 })
        {
            row.Artifact = artifact;
        }

        var ver = await _db.ModelVersions.FindAsync([model.Id], ct);
        if (ver is null)
        {
            var nextNo = 1;
            if (!string.IsNullOrEmpty(row.DatasetId))
            {
                var max = await _db.ModelVersions.Where(v => v.DatasetId == row.DatasetId)
                    .Select(v => (int?)v.VersionNumber).MaxAsync(ct);
                nextNo = (max ?? 0) + 1;
            }

            ver = new ModelVersion
            {
                Id = model.Id,
                DatasetId = row.DatasetId,
                VersionNumber = nextNo,
                Status = ModelStatuses.Draft,
                TrainedByUserId = trainedByUserId,
                TrainedAt = DateTimeOffset.UtcNow
            };
            _db.ModelVersions.Add(ver);
        }

        ver.Algorithm = model.Algorithm;
        ver.MetricsJson = row.MetricsJson;
        ver.ImportancesJson = row.ImportancesJson;
        if (artifact is { Length: > 0 })
        {
            ver.Artifact = artifact;
        }

        if (string.IsNullOrEmpty(ver.TrainedByUserId) && trainedByUserId is not null)
        {
            ver.TrainedByUserId = trainedByUserId;
        }

        if (string.IsNullOrEmpty(ver.Status))
        {
            ver.Status = ModelStatuses.Draft;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<TrainJob?> GetJobAsync(string id, CancellationToken ct)
        => await _db.TrainJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<ImportancesResponse?> CachedImportancesAsync(string modelId, CancellationToken ct)
    {
        var job = await GetJobAsync(modelId, ct);
        if (string.IsNullOrWhiteSpace(job?.ImportancesJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ImportancesResponse>(job.ImportancesJson, Json);
    }

    public async Task RememberWhatIfAsync(string modelId, WhatIfRequest req, WhatIfResponse res, CancellationToken ct)
    {
        _db.WhatIfNotes.Add(new WhatIfNote
        {
            Id = Guid.NewGuid().ToString("N"),
            ModelId = modelId,
            CreatedAt = DateTimeOffset.UtcNow,
            RequestJson = JsonSerializer.Serialize(req, Json),
            ResponseJson = JsonSerializer.Serialize(res, Json)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ModelResponse>> ListVersionsAsync(string datasetId, CancellationToken ct)
    {
        var jobs = await _db.TrainJobs.AsNoTracking().Where(j => j.DatasetId == datasetId).ToListAsync(ct);
        var vers = await _db.ModelVersions.AsNoTracking().Where(v => v.DatasetId == datasetId)
            .OrderByDescending(v => v.VersionNumber).ToListAsync(ct);
        var users = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        return vers.Select(v =>
        {
            var job = jobs.FirstOrDefault(j => j.Id == v.Id);
            var dto = job is null
                ? new ModelResponse { Id = v.Id, DatasetId = v.DatasetId, Algorithm = v.Algorithm }
                : ToModelDto(job, v, users);
            return dto;
        }).ToList();
    }

    public async Task<ModelVersion?> GetVersionAsync(string id, CancellationToken ct)
        => await _db.ModelVersions.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task ApproveAsync(string id, string approverId, string? note, CancellationToken ct)
    {
        var ver = await _db.ModelVersions.FirstOrDefaultAsync(v => v.Id == id, ct)
                  ?? throw new InvalidOperationException("versionNotFound");
        var prev = await _db.ModelVersions
            .Where(v => v.DatasetId == ver.DatasetId && v.Status == ModelStatuses.Approved && v.Id != id)
            .ToListAsync(ct);
        foreach (var p in prev)
        {
            p.Status = ModelStatuses.Retired;
        }

        ver.Status = ModelStatuses.Approved;
        ver.ApprovedByUserId = approverId;
        ver.ApprovedAt = DateTimeOffset.UtcNow;
        ver.Note = note;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(string id, string approverId, string? note, CancellationToken ct)
    {
        var ver = await _db.ModelVersions.FirstOrDefaultAsync(v => v.Id == id, ct)
                  ?? throw new InvalidOperationException("versionNotFound");
        ver.Status = ModelStatuses.Rejected;
        ver.ApprovedByUserId = approverId;
        ver.ApprovedAt = DateTimeOffset.UtcNow;
        ver.Note = note;
        await _db.SaveChangesAsync(ct);
    }

    public ModelResponse ToModelDto(TrainJob job, ModelVersion? ver = null, IReadOnlyDictionary<string, string>? users = null)
    {
        users ??= new Dictionary<string, string>();
        string? trainer = null;
        if (ver?.TrainedByUserId is { } uid)
        {
            trainer = users.TryGetValue(uid, out var name) ? name : uid;
        }

        var metrics = JsonSerializer.Deserialize<ModelMetricsDto>(job.MetricsJson, Json) ?? new ModelMetricsDto();
        return new ModelResponse
        {
            Id = job.Id,
            DatasetId = job.DatasetId,
            TargetColumn = job.TargetColumn,
            Task = job.Task,
            Algorithm = job.Algorithm,
            FeatureNames = JsonSerializer.Deserialize<List<string>>(job.FeatureNamesJson, Json) ?? [],
            Metrics = metrics,
            TaskKind = metrics.TaskKind,
            Classes = metrics.Classes ?? [],
            VersionNumber = ver?.VersionNumber,
            Status = ver?.Status,
            TrainedBy = trainer,
            Note = ver?.Note
        };
    }

    public async Task<ModelResponse> ToModelDtoAsync(TrainJob job, CancellationToken ct)
    {
        var ver = await _db.ModelVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == job.Id, ct);
        var users = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        return ToModelDto(job, ver, users);
    }

    private static DatasetResponse ToDto(ProcessDataset row)
        => new()
        {
            Id = row.Id,
            Name = row.Name,
            RowCount = row.RowCount,
            Columns = JsonSerializer.Deserialize<List<ColumnDto>>(row.ColumnsJson, Json) ?? [],
            PreviewRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(row.PreviewJson, Json) ?? [],
            Overview = string.IsNullOrWhiteSpace(row.OverviewJson)
                ? null
                : JsonSerializer.Deserialize<DatasetOverviewDto>(row.OverviewJson, Json)
        };

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
