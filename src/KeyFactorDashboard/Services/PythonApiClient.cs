using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KeyFactorDashboard.Models;

namespace KeyFactorDashboard.Services;

public sealed class PythonApiClient : IPythonApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public PythonApiClient(HttpClient http) => _http = http;

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<DatasetResponse> UploadCsvAsync(Stream content, string fileName, string? name, CancellationToken cancellationToken = default, string? reuseId = null)
        => UploadCsvCore(content, fileName, name, reuseId, cancellationToken);

    private async Task<DatasetResponse> UploadCsvCore(Stream content, string fileName, string? name, string? reuseId, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(streamContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(name))
        {
            form.Add(new StringContent(name), "name");
        }

        if (!string.IsNullOrWhiteSpace(reuseId))
        {
            form.Add(new StringContent(reuseId), "id");
        }

        return await SendAsync<DatasetResponse>(HttpMethod.Post, "/datasets", form, cancellationToken);
    }

    public Task<DatasetResponse> UploadJsonAsync(JsonDatasetRequest request, CancellationToken cancellationToken = default)
        => SendAsync<DatasetResponse>(HttpMethod.Post, "/datasets/json", request, cancellationToken);

    public Task<DatasetResponse> LoadSampleAsync(CancellationToken cancellationToken = default)
        => SendAsync<DatasetResponse>(HttpMethod.Post, "/datasets/sample", null, cancellationToken);

    public Task<DatasetResponse> LoadSampleOkngAsync(CancellationToken cancellationToken = default)
        => SendAsync<DatasetResponse>(HttpMethod.Post, "/datasets/sample-okng", null, cancellationToken);

    public Task<DatasetResponse> GetDatasetAsync(string id, string? target, CancellationToken cancellationToken = default)
    {
        var url = $"/datasets/{Uri.EscapeDataString(id)}";
        if (!string.IsNullOrWhiteSpace(target))
        {
            url += $"?target={Uri.EscapeDataString(target)}";
        }

        return SendAsync<DatasetResponse>(HttpMethod.Get, url, null, cancellationToken);
    }

    public Task<ModelResponse> TrainAsync(TrainRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ModelResponse>(HttpMethod.Post, "/models/train", request, cancellationToken);

    public Task<ModelResponse> GetModelAsync(string id, CancellationToken cancellationToken = default)
        => SendAsync<ModelResponse>(HttpMethod.Get, $"/models/{Uri.EscapeDataString(id)}", null, cancellationToken);

    public Task<ImportancesResponse> GetImportancesAsync(string modelId, int? topN, CancellationToken cancellationToken = default)
    {
        var url = $"/models/{Uri.EscapeDataString(modelId)}/importances";
        if (topN is > 0)
        {
            url += $"?topN={topN}";
        }

        return SendAsync<ImportancesResponse>(HttpMethod.Get, url, null, cancellationToken);
    }

    public Task<DependenceResponse> GetDependenceAsync(string modelId, string feature, string? colorBy, CancellationToken cancellationToken = default, string? className = null)
    {
        var url = $"/models/{Uri.EscapeDataString(modelId)}/dependence?feature={Uri.EscapeDataString(feature)}";
        if (!string.IsNullOrWhiteSpace(colorBy))
        {
            url += $"&colorBy={Uri.EscapeDataString(colorBy)}";
        }

        if (!string.IsNullOrWhiteSpace(className))
        {
            url += $"&class={Uri.EscapeDataString(className)}";
        }

        return SendAsync<DependenceResponse>(HttpMethod.Get, url, null, cancellationToken);
    }

    public Task<WhatIfResponse> WhatIfAsync(string modelId, WhatIfRequest request, CancellationToken cancellationToken = default)
        => SendAsync<WhatIfResponse>(HttpMethod.Post, $"/models/{Uri.EscapeDataString(modelId)}/whatif", request, cancellationToken);

    public Task<RowsResponse> GetRowsAsync(string modelId, CancellationToken cancellationToken = default)
        => SendAsync<RowsResponse>(HttpMethod.Get, $"/models/{Uri.EscapeDataString(modelId)}/rows", null, cancellationToken);

    public Task<ModelResponse> RestoreAsync(string artifactBase64, string? modelId = null, CancellationToken cancellationToken = default)
        => SendAsync<ModelResponse>(HttpMethod.Post, "/models/restore", new RestoreModelRequest { Id = modelId, ArtifactBase64 = artifactBase64 }, cancellationToken);

    public static ImportancesResponse DeserializeImportances(string json)
        => JsonSerializer.Deserialize<ImportancesResponse>(json, JsonOptions)
           ?? throw new InvalidOperationException("Importances payload was empty.");

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is HttpContent content)
        {
            request.Content = content;
        }
        else if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PythonApiException(503, "pythonUnavailable", "The analysis service is not reachable.");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadError(payload);
            throw new PythonApiException(
                (int)response.StatusCode,
                error?.ErrorCode,
                error?.Message ?? $"Analysis service returned {(int)response.StatusCode}.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new PythonApiException(502, "emptyPythonResponse", "Analysis service returned an empty body.");
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
               ?? throw new PythonApiException(502, "invalidPythonResponse", "Analysis service returned an unexpected payload.");
    }

    private static PythonErrorBody? TryReadError(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<PythonErrorBody>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
