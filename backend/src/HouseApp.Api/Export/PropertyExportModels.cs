using HouseApp.Api.Models;

namespace HouseApp.Api.Export;

// The shape of data.json in a property export. Deliberately its own set of types rather than the
// API DTOs: the export is a file people keep for years and may feed into something else, so a DTO
// change made for the UI must not silently change it. Bump FormatVersion on any breaking change.
//
// What's left out is as deliberate as what's in: user ids are resolved to display names (an id
// means nothing outside this database), and Drive folder ids, the connecting user and every token
// are internal plumbing. Entity ids are kept, since they're what links a document to its project and
// a project to its component.

public record PropertyExport(
    int FormatVersion,
    DateTimeOffset ExportedAt,
    string? ExportedBy,
    ExportedProperty Property,
    List<ExportedComponent> Components,
    List<ExportedProject> Projects,
    List<ExportedValuation> Valuations,
    List<ExportedBudget> Budgets,
    List<ExportedDocument> Documents);

public record ExportedProperty(
    string Id,
    string Nickname,
    string Address,
    string? Address2,
    string? PostalCode,
    string? City,
    string? Country,
    string? PropertyDesignation,
    int? YearBuilt,
    PropertyType? Type,
    double? Latitude,
    double? Longitude,
    DateOnly PurchaseDate,
    decimal PurchasePrice,
    bool IsDemo,
    bool ComponentsCustomized,
    List<string> Members,
    string? GoogleDriveFolderUrl,
    DateTimeOffset CreatedAt);

public record ExportedComponent(string Id, string Name, int? RecommendedIntervalMonths);

public record ExportedProject(
    string Id,
    string Name,
    string? Description,
    string? Notes,
    WorkType WorkType,
    string ComponentId,
    string? ComponentName,
    ProjectStatus Status,
    ProjectPriority Priority,
    bool IsUrgent,
    bool ExcludeFromMaintenanceSchedule,
    DateOnly? PlannedStartDate,
    DateOnly? ActualStartDate,
    DateOnly? CompletedDate,
    int? EstimatedDurationDays,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal? EstimatedValueIncrease,
    int? ExpectedLifespanYears,
    decimal? EnergyEfficiencyGainPercent,
    ExportedContractor? Contractor,
    List<ExportedCost> Costs,
    List<ExportedMilestone> Milestones,
    string? CreatedBy,
    DateTimeOffset CreatedAt);

public record ExportedContractor(
    string Name,
    string? Phone,
    string? Email,
    string? Website,
    decimal? QuotedPrice,
    DateOnly? QuotedDate,
    string? Notes);

public record ExportedCost(
    string Id,
    CostType Type,
    string? Description,
    decimal Amount,
    DateOnly DateIncurred,
    bool IsBudgeted);

public record ExportedMilestone(string Id, string Description, DateOnly? PlannedDate, DateOnly? CompletedDate);

public record ExportedValuation(
    string Id,
    DateOnly Date,
    decimal Value,
    string? Source,
    string? Notes,
    string? CreatedBy,
    DateTimeOffset CreatedAt);

public record ExportedBudget(
    int Year,
    decimal MaintenanceBudget,
    decimal RenovationBudget,
    decimal InvestmentBudget,
    decimal PurchaseBudget);

/// <param name="File">Path inside the zip, for Blob documents. Null for Drive, whose files stay in Drive.</param>
/// <param name="FileMissing">The blob couldn't be read when the export ran, so <paramref name="File"/> isn't in the zip.</param>
public record ExportedDocument(
    string Id,
    string? ProjectId,
    string? Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentCategory Category,
    DateOnly Date,
    DocumentStorageKind Storage,
    string? File,
    string? DriveWebViewLink,
    bool FileMissing,
    string? UploadedBy,
    DateTimeOffset UploadedAt);

/// <summary>A Blob file to copy into the zip. The blob path itself never reaches data.json.</summary>
public record ExportFile(string DocumentId, string ZipPath, string BlobPath);

public record PropertyExportBundle(PropertyExport Data, List<ExportFile> Files);
