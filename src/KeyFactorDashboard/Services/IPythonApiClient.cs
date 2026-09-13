using KeyFactorDashboard.Models;

namespace KeyFactorDashboard.Services;

public interface IPythonApiClient
{
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
    Task<DatasetResponse> UploadCsvAsync(Stream content, string fileName, string? name, CancellationToken cancellationToken = default, string? reuseId = null);
    Task<DatasetResponse> UploadJsonAsync(JsonDatasetRequest request, CancellationToken cancellationToken = default);
    Task<DatasetResponse> LoadSampleAsync(CancellationToken cancellationToken = default);
    Task<DatasetResponse> LoadSampleOkngAsync(CancellationToken cancellationToken = default);
    Task<DatasetResponse> GetDatasetAsync(string id, string? target, CancellationToken cancellationToken = default);
    Task<ModelResponse> TrainAsync(TrainRequest request, CancellationToken cancellationToken = default);
    Task<ModelResponse> GetModelAsync(string id, CancellationToken cancellationToken = default);
    Task<ImportancesResponse> GetImportancesAsync(string modelId, int? topN, CancellationToken cancellationToken = default);
    Task<DependenceResponse> GetDependenceAsync(string modelId, string feature, string? colorBy, CancellationToken cancellationToken = default, string? className = null);
    Task<WhatIfResponse> WhatIfAsync(string modelId, WhatIfRequest request, CancellationToken cancellationToken = default);
    Task<RowsResponse> GetRowsAsync(string modelId, CancellationToken cancellationToken = default);
    Task<ModelResponse> RestoreAsync(string artifactBase64, string? modelId = null, CancellationToken cancellationToken = default);
}
