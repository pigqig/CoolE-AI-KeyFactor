using System.Text.Json.Serialization;

namespace KeyFactorDashboard.Models;

public sealed class ColumnDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "numeric";
    public int MissingCount { get; set; }
}

public sealed class InsightArgsDto
{
    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

public sealed class InsightDto
{
    public string? Feature { get; set; }
    public int? Rank { get; set; }
    public double? Share { get; set; }
    public double? Importance { get; set; }
    public string? Direction { get; set; }
    public double? Correlation { get; set; }
    public string InsightCode { get; set; } = "";
    public string Summary { get; set; } = "";
    public Dictionary<string, object?>? Args { get; set; }
    public double? Delta { get; set; }
    public double? DeltaPct { get; set; }
    public double? Slope { get; set; }
    public double? SteepestFrom { get; set; }
    public double? SteepestTo { get; set; }
}

public sealed class DatasetOverviewDto
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public int NumericCount { get; set; }
    public int CategoricalCount { get; set; }
    public Dictionary<string, int>? MissingCounts { get; set; }
    public List<string>? MissingNotes { get; set; }
    public string? TargetColumn { get; set; }
    public string InsightCode { get; set; } = "";
    public string Summary { get; set; } = "";
    public Dictionary<string, object?>? Args { get; set; }
}

public sealed class DatasetResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int RowCount { get; set; }
    public List<ColumnDto> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> PreviewRows { get; set; } = [];
    public DatasetOverviewDto? Overview { get; set; }
}

public sealed class JsonDatasetRequest
{
    public string? Id { get; set; }

    /// <example>line-A-rework</example>
    public string? Name { get; set; }

    /// <example>["Temperature","Pressure","TargetConductivity"]</example>
    public List<object> Columns { get; set; } = [];

    /// <example>[[180,2.1,12.4],[176,2.3,11.9]]</example>
    public List<object> Rows { get; set; } = [];
}

public sealed class TrainRequest
{
    /// <example>0f1e2d3c4b5a69788776655443322110</example>
    public string DatasetId { get; set; } = "";

    /// <example>TargetConductivity</example>
    public string TargetColumn { get; set; } = "";

    /// <summary>gbr (gradient boosting) or rf (random forest).</summary>
    public string? Algorithm { get; set; } = "gbr";

    /// <summary>
    /// <c>auto</c> (default): 2 unique target values → binary classification, otherwise regression.
    /// Explicit: <c>regression</c>, <c>binary</c>, or <c>multiclass</c>.
    /// </summary>
    /// <example>auto</example>
    public string? Task { get; set; } = "auto";
}

public sealed class ModelMetricsDto
{
    public string? Task { get; set; }
    public double? RSquared { get; set; }
    public double? Rmse { get; set; }
    public double? Mae { get; set; }
    public double? Accuracy { get; set; }
    public double? Auc { get; set; }
    public double? F1 { get; set; }
    public string? Algorithm { get; set; }
    public int? NRows { get; set; }
    public int? NFeatures { get; set; }
    public string? TargetColumn { get; set; }
    public string? TaskKind { get; set; }
    public List<string>? Classes { get; set; }
    public ConfusionMatrixDto? Confusion { get; set; }
    public List<ClassMetricDto>? ClassMetrics { get; set; }
}

public sealed class ConfusionMatrixDto
{
    public List<string> Labels { get; set; } = [];
    public List<List<int>> Matrix { get; set; } = [];
}

public sealed class ClassMetricDto
{
    public string Label { get; set; } = "";
    public int Support { get; set; }
    public double? SupportPct { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
}

public sealed class ModelResponse
{
    public string Id { get; set; } = "";
    public string? DatasetId { get; set; }
    public string TargetColumn { get; set; } = "";
    public string Task { get; set; } = "";
    public string Algorithm { get; set; } = "";
    public string? TaskKind { get; set; }
    public List<string> Classes { get; set; } = [];
    public List<string> FeatureNames { get; set; } = [];
    public ModelMetricsDto Metrics { get; set; } = new();
    public DatasetOverviewDto? Overview { get; set; }
    public int? VersionNumber { get; set; }
    public string? Status { get; set; }
    public string? TrainedBy { get; set; }
    public string? Note { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtifactBase64 { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class MeResponse
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public MeResponse User { get; set; } = new();
}

public sealed class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Viewer";
}

public sealed class UserRowDto
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AssignRoleRequest
{
    public string Role { get; set; } = "";
}

public sealed class VersionNoteRequest
{
    public string? Note { get; set; }
}

public sealed class AuditRowDto
{
    public string Id { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
}

public sealed class RestoreModelRequest
{
    public string? Id { get; set; }
    public string ArtifactBase64 { get; set; } = "";
}

public sealed class FeatureImportanceDto
{
    public string Feature { get; set; } = "";
    public double Importance { get; set; }
    public double Share { get; set; }
    public int Rank { get; set; }
    public string? Direction { get; set; }
    public double? Correlation { get; set; }
    public string? Description { get; set; }
}

public sealed class ImportancesResponse
{
    public string Method { get; set; } = "permutation";
    public string? TargetColumn { get; set; }
    public List<FeatureImportanceDto> Importances { get; set; } = [];
    public List<InsightDto> Insights { get; set; } = [];
    public List<InsightDto> ClassInsights { get; set; } = [];
}

public sealed class DependencePointDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public object? Color { get; set; }
    public string? XLabel { get; set; }
}

public sealed class DependenceResponse
{
    public string Feature { get; set; } = "";
    public string FeatureType { get; set; } = "numeric";
    public string? ColorBy { get; set; }
    public string? TargetColumn { get; set; }
    public string? ClassLabel { get; set; }
    public List<DependencePointDto> Pdp { get; set; } = [];
    public List<DependencePointDto> Points { get; set; } = [];
    public InsightDto? Insight { get; set; }
}

public sealed class WhatIfRequest
{
    public int? RowIndex { get; set; }
    public Dictionary<string, object?>? Row { get; set; }
    public Dictionary<string, object?>? Edits { get; set; }
}

public sealed class ContributionDto
{
    public string Feature { get; set; } = "";
    public double Delta { get; set; }
}

public sealed class ProbabilityDto
{
    public string Label { get; set; } = "";
    public double Probability { get; set; }
}

public sealed class WhatIfResponse
{
    public int? RowIndex { get; set; }
    public string Task { get; set; } = "";
    public string? TargetColumn { get; set; }
    public double Baseline { get; set; }
    public double Prediction { get; set; }
    public string? PredictedClass { get; set; }
    public string? BaselineClass { get; set; }
    public double MeanPrediction { get; set; }
    public Dictionary<string, object?>? Row { get; set; }
    public Dictionary<string, object?>? Edits { get; set; }
    public List<ContributionDto> Contributions { get; set; } = [];
    public List<ProbabilityDto>? Probabilities { get; set; }
    public InsightDto? Narrative { get; set; }
    public InsightDto? Insight { get; set; }
}

public sealed class RowPredictionDto
{
    public int Index { get; set; }
    public object? Actual { get; set; }
    public double Predicted { get; set; }
    public double? Residual { get; set; }
    public Dictionary<string, object?> Values { get; set; } = [];
}

public sealed class RowsResponse
{
    public List<RowPredictionDto> Rows { get; set; } = [];
    public int RowCount { get; set; }
    public string? Task { get; set; }
    public string? TargetColumn { get; set; }
}

public sealed class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Python { get; set; } = "unknown";
}

public sealed class PythonErrorBody
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
