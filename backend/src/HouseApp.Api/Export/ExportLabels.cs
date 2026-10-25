using HouseApp.Api.Models;

namespace HouseApp.Api.Export;

/// <summary>
/// Swedish labels for the export's Excel file. A backend copy of <c>frontend/src/utils/labels.ts</c>
/// — the projects share no code — so keep the two in step. data.json keeps the English enum values,
/// which are the contract; only the spreadsheet a person reads is translated. A test fails if an
/// appended enum value has no label here.
/// </summary>
public static class ExportLabels
{
    public static readonly IReadOnlyDictionary<WorkType, string> WorkTypes = new Dictionary<WorkType, string>
    {
        [WorkType.Maintenance] = "Underhåll",
        [WorkType.Renovation] = "Renovering",
        [WorkType.Investment] = "Nyinvestering",
        [WorkType.Purchase] = "Inköp",
    };

    public static readonly IReadOnlyDictionary<ProjectStatus, string> Statuses = new Dictionary<ProjectStatus, string>
    {
        [ProjectStatus.Planned] = "Planerad",
        [ProjectStatus.InProgress] = "Pågående",
        [ProjectStatus.Completed] = "Slutförd",
        [ProjectStatus.OnHold] = "Uppskjuten",
        [ProjectStatus.Cancelled] = "Avbruten",
    };

    public static readonly IReadOnlyDictionary<ProjectPriority, string> Priorities = new Dictionary<ProjectPriority, string>
    {
        [ProjectPriority.Low] = "Låg",
        [ProjectPriority.Medium] = "Medel",
        [ProjectPriority.High] = "Hög",
        [ProjectPriority.Critical] = "Kritisk",
    };

    public static readonly IReadOnlyDictionary<CostType, string> CostTypes = new Dictionary<CostType, string>
    {
        [CostType.Materials] = "Material",
        [CostType.Labor] = "Arbetskraft",
        [CostType.Tools] = "Verktyg",
        [CostType.Permits] = "Tillstånd",
        [CostType.Other] = "Övrigt",
    };

    public static readonly IReadOnlyDictionary<PropertyType, string> PropertyTypes = new Dictionary<PropertyType, string>
    {
        [PropertyType.House] = "Villa",
        [PropertyType.Apartment] = "Lägenhet",
        [PropertyType.Townhouse] = "Radhus",
        [PropertyType.Cottage] = "Fritidshus",
        [PropertyType.Other] = "Övrigt",
    };

    public static readonly IReadOnlyDictionary<DocumentCategory, string> DocumentCategories = new Dictionary<DocumentCategory, string>
    {
        [DocumentCategory.Deed] = "Lagfart",
        [DocumentCategory.Warranty] = "Garanti",
        [DocumentCategory.Receipt] = "Kvitto",
        [DocumentCategory.Quote] = "Offert",
        [DocumentCategory.Invoice] = "Faktura",
        [DocumentCategory.Photo] = "Foto",
        [DocumentCategory.Other] = "Övrigt",
    };

    public static readonly IReadOnlyDictionary<DocumentStorageKind, string> StorageKinds = new Dictionary<DocumentStorageKind, string>
    {
        [DocumentStorageKind.Blob] = "HusTracker",
        [DocumentStorageKind.Drive] = "Google Drive",
    };
}
