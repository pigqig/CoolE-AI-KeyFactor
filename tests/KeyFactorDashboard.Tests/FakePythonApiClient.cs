using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;

namespace KeyFactorDashboard.Tests;

/// <summary>In-memory stand-in so WebApplicationFactory can exercise /api/v1 without uvicorn.</summary>
public sealed class FakePythonApiClient : IPythonApiClient
{
    private readonly Dictionary<string, DatasetResponse> _sets = new();
    private readonly Dictionary<string, ModelResponse> _models = new();

    public Task<bool> HealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<DatasetResponse> UploadCsvAsync(Stream content, string fileName, string? name, CancellationToken cancellationToken = default, string? reuseId = null)
        => Task.FromResult(PutSet(name ?? fileName, DemoColumns(), DemoRows(), reuseId));

    public Task<DatasetResponse> UploadJsonAsync(JsonDatasetRequest request, CancellationToken cancellationToken = default)
    {
        var cols = request.Columns.Select(c => c?.ToString() ?? "col").ToList();
        if (cols.Count == 0)
        {
            cols = ["Temperature", "TargetConductivity"];
        }

        return Task.FromResult(PutSet(
            request.Name ?? "json",
            cols.Select(n => new ColumnDto
            {
                Name = n,
                Type = n == "LineId" ? "categorical" : "numeric"
            }).ToList(),
            request.Rows.Count == 0 ? DemoRows() : request.Rows,
            request.Id));
    }

    public Task<DatasetResponse> LoadSampleAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PutSet("process-quality.csv", DemoColumns(), DemoRows()));

    public Task<DatasetResponse> LoadSampleOkngAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PutSet("process-okng.csv", OkngColumns(), OkngRows()));

    public Task<DatasetResponse> GetDatasetAsync(string id, string? target, CancellationToken cancellationToken = default)
    {
        if (!_sets.TryGetValue(id, out var ds))
        {
            throw new PythonApiException(404, "datasetNotFound", "Dataset was not found.");
        }

        if (!string.IsNullOrWhiteSpace(target) && ds.Overview is not null)
        {
            ds.Overview.TargetColumn = target;
            ds.Overview.InsightCode = "datasetOverviewWithTarget";
            ds.Overview.Summary = $"This dataset has {ds.RowCount} process records. Target is {target}.";
        }

        return Task.FromResult(ds);
    }

    public Task<ModelResponse> TrainAsync(TrainRequest request, CancellationToken cancellationToken = default)
    {
        if (!_sets.ContainsKey(request.DatasetId))
        {
            throw new PythonApiException(404, "datasetNotFound", "Dataset was not found.");
        }

        if (string.Equals(request.Task, "multiclass", StringComparison.OrdinalIgnoreCase))
        {
            throw new PythonApiException(400, "invalidMulticlassTarget", "Multiclass needs at least 3 target values; found 2.");
        }

        var classify = string.Equals(request.Task, "binary", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(request.TargetColumn, "QualityResult", StringComparison.OrdinalIgnoreCase);

        var model = classify ? ClassModel(request) : new ModelResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            DatasetId = request.DatasetId,
            TargetColumn = request.TargetColumn,
            Task = "regression",
            Algorithm = request.Algorithm == "rf" ? "random_forest" : "gradient_boosting",
            FeatureNames = ["Temperature", "Pressure", "AdditivePct"],
            Metrics = new ModelMetricsDto
            {
                Task = "regression",
                RSquared = 0.81,
                Rmse = 0.62,
                Mae = 0.48,
                TargetColumn = request.TargetColumn,
                NRows = 8,
                NFeatures = 3
            }
        };
        _models[model.Id] = model;
        return Task.FromResult(model);
    }

    public Task<ModelResponse> GetModelAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_models.TryGetValue(id, out var m) ? m : throw new PythonApiException(404, "modelNotFound", "Model was not found."));

    public Task<ImportancesResponse> GetImportancesAsync(string modelId, int? topN, CancellationToken cancellationToken = default)
    {
        if (!_models.ContainsKey(modelId))
        {
            throw new PythonApiException(404, "modelNotFound", "Model was not found.");
        }

        var items = new List<FeatureImportanceDto>
        {
            new() { Feature = "Temperature", Importance = 0.42, Share = 0.42, Rank = 1, Direction = "up" },
            new() { Feature = "AdditivePct", Importance = 0.31, Share = 0.31, Rank = 2, Direction = "up" },
            new() { Feature = "Pressure", Importance = 0.12, Share = 0.12, Rank = 3, Direction = "flat" }
        };
        if (topN is > 0)
        {
            items = items.Take(topN.Value).ToList();
        }

        return Task.FromResult(new ImportancesResponse
        {
            Method = "permutation",
            TargetColumn = "TargetConductivity",
            Importances = items,
            Insights = items.Select(i => new InsightDto
            {
                Feature = i.Feature,
                Rank = i.Rank,
                Share = i.Share,
                Importance = i.Importance,
                Direction = i.Direction,
                InsightCode = i.Rank == 1 ? "topFactor" : "highFactor",
                Summary = $"{i.Feature} is a key process factor (about {i.Share:P0}) for TargetConductivity."
            }).ToList()
        });
    }

    public Task<DependenceResponse> GetDependenceAsync(string modelId, string feature, string? colorBy, CancellationToken cancellationToken = default, string? className = null)
        => Task.FromResult(new DependenceResponse
        {
            Feature = feature,
            FeatureType = "numeric",
            ColorBy = colorBy,
            TargetColumn = "TargetConductivity",
            ClassLabel = className,
            Pdp = [new() { X = 160, Y = 10 }, new() { X = 200, Y = 14 }],
            Insight = new InsightDto
            {
                InsightCode = "dependenceNumeric",
                Summary = $"The X axis is {feature}. Higher values tend to raise predicted conductivity.",
                Direction = "up"
            }
        });

    public Task<WhatIfResponse> WhatIfAsync(string modelId, WhatIfRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new WhatIfResponse
        {
            RowIndex = request.RowIndex ?? 0,
            Task = "regression",
            TargetColumn = "TargetConductivity",
            Baseline = 12.3,
            Prediction = 13.1,
            MeanPrediction = 12.5,
            Contributions = [new() { Feature = "Temperature", Delta = 0.6 }],
            Narrative = new InsightDto
            {
                InsightCode = "whatIfChanged",
                Summary = "Raising Temperature moved predicted TargetConductivity from 12.3 to 13.1 (+6.5%)."
            },
            Insight = new InsightDto
            {
                InsightCode = "whatIfChanged",
                Summary = "Raising Temperature moved predicted TargetConductivity from 12.3 to 13.1 (+6.5%)."
            }
        });

    public Task<ModelResponse> RestoreAsync(string artifactBase64, string? modelId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new ModelResponse { Id = modelId ?? Guid.NewGuid().ToString("N"), TargetColumn = "TargetConductivity", Task = "regression" });

    public Task<RowsResponse> GetRowsAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RowsResponse
        {
            RowCount = 2,
            Task = "regression",
            TargetColumn = "TargetConductivity",
            Rows =
            [
                new() { Index = 0, Actual = 12.1, Predicted = 12.0, Residual = -0.1, Values = new() { ["Temperature"] = 178 } }
            ]
        });

    private DatasetResponse PutSet(string name, List<ColumnDto> columns, List<object> rows, string? reuseId = null)
    {
        var id = string.IsNullOrWhiteSpace(reuseId) ? Guid.NewGuid().ToString("N") : reuseId;
        var preview = new List<Dictionary<string, object?>>();
        foreach (var row in rows.Take(8))
        {
            if (row is Dictionary<string, object?> already)
            {
                preview.Add(already);
            }
            else
            {
                preview.Add(new Dictionary<string, object?> { ["Temperature"] = 180d, ["TargetConductivity"] = 12.4d });
            }
        }

        var ds = new DatasetResponse
        {
            Id = id,
            Name = name,
            RowCount = Math.Max(rows.Count, 8),
            Columns = columns,
            PreviewRows = preview,
            Overview = new DatasetOverviewDto
            {
                RowCount = Math.Max(rows.Count, 8),
                ColumnCount = columns.Count,
                NumericCount = columns.Count(c => c.Type == "numeric"),
                CategoricalCount = columns.Count(c => c.Type != "numeric"),
                InsightCode = "datasetOverview",
                Summary = $"This dataset has {Math.Max(rows.Count, 8)} process records and {columns.Count} parameters."
            }
        };
        _sets[id] = ds;
        return ds;
    }

    private static ModelResponse ClassModel(TrainRequest request) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        DatasetId = request.DatasetId,
        TargetColumn = request.TargetColumn,
        Task = "classification",
        TaskKind = "binary",
        Classes = ["NG", "OK"],
        Algorithm = request.Algorithm == "rf" ? "random_forest" : "gradient_boosting",
        FeatureNames = ["Temperature", "Pressure"],
        Metrics = new ModelMetricsDto
        {
            Task = "classification",
            TaskKind = "binary",
            Accuracy = 0.88,
            F1 = 0.86,
            Auc = 0.91,
            TargetColumn = request.TargetColumn,
            NRows = 12,
            NFeatures = 2,
            Classes = ["NG", "OK"],
            Confusion = new ConfusionMatrixDto
            {
                Labels = ["NG", "OK"],
                Matrix = [[5, 1], [1, 5]]
            }
        }
    };

    private static List<ColumnDto> DemoColumns() =>
    [
        new() { Name = "Temperature", Type = "numeric" },
        new() { Name = "Pressure", Type = "numeric" },
        new() { Name = "TargetConductivity", Type = "numeric" }
    ];

    private static List<object> DemoRows() =>
    [
        new Dictionary<string, object?> { ["Temperature"] = 180d, ["Pressure"] = 2.1d, ["TargetConductivity"] = 12.4d }
    ];

    private static List<ColumnDto> OkngColumns() =>
    [
        new() { Name = "Temperature", Type = "numeric" },
        new() { Name = "Pressure", Type = "numeric" },
        new() { Name = "QualityResult", Type = "categorical" }
    ];

    private static List<object> OkngRows() =>
    [
        new Dictionary<string, object?> { ["Temperature"] = 180d, ["Pressure"] = 2.1d, ["QualityResult"] = "OK" }
    ];
}
