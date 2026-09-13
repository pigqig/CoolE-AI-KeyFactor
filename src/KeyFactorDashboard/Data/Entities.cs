namespace KeyFactorDashboard.Data;

public sealed class ProcessDataset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public int RowCount { get; set; }
    public string ColumnsJson { get; set; } = "[]";
    public string PreviewJson { get; set; } = "[]";
    public string? OverviewJson { get; set; }
    public string PayloadKind { get; set; } = "csv";
    public byte[] Payload { get; set; } = [];
}

public sealed class TrainJob
{
    public string Id { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string TargetColumn { get; set; } = "";
    public string Algorithm { get; set; } = "gbr";
    public string Task { get; set; } = "regression";
    public DateTimeOffset CreatedAt { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public string FeatureNamesJson { get; set; } = "[]";
    public string? ImportancesJson { get; set; }
    public byte[]? Artifact { get; set; }
    public ProcessDataset? Dataset { get; set; }
}

public sealed class WhatIfNote
{
    public string Id { get; set; } = "";
    public string ModelId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string RequestJson { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";
}

public sealed class RoleRecord
{
    public string Name { get; set; } = "";
}

public sealed class PlantUser
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Viewer";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class ModelStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Retired = "Retired";
}

public sealed class ModelVersion
{
    public string Id { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public int VersionNumber { get; set; }
    public string Algorithm { get; set; } = "";
    public string MetricsJson { get; set; } = "{}";
    public string? ImportancesJson { get; set; }
    public byte[]? Artifact { get; set; }
    public string? TrainedByUserId { get; set; }
    public DateTimeOffset TrainedAt { get; set; }
    public string Status { get; set; } = ModelStatuses.Draft;
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Note { get; set; }
}

public sealed class AuditEvent
{
    public string Id { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
}
