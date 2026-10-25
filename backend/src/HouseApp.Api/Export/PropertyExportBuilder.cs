using HouseApp.Api.Data;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Export;

/// <summary>
/// Gathers everything belonging to one property into the export model. **A new per-property
/// container or field needs adding here** — same reminder as <c>PropertiesController.Delete</c>, and
/// for the same reason: nothing else will notice it's missing.
/// </summary>
public class PropertyExportBuilder(AppDbContext db)
{
    public const int FormatVersion = 1;

    public const string DocumentsFolder = "dokument";

    public async Task<PropertyExportBundle> BuildAsync(Property property, string exportedByUserId)
    {
        var id = property.Id;

        // Single-partition queries, the same ones the delete cascade uses.
        var projects = await db.Projects.Where(p => p.PropertyId == id).ToListAsync();
        var valuations = await db.ValuationEntries.Where(v => v.PropertyId == id).ToListAsync();
        var documents = await db.Documents.Where(d => d.PropertyId == id).ToListAsync();
        var budgets = await db.Budgets.Where(b => b.PropertyId == id).ToListAsync();
        var components = await db.GetEffectiveComponentsAsync(property);

        // One read for every name the export needs — the users container holds a handful of rows.
        var users = (await db.Users.ToListAsync()).ToDictionary(u => u.Id, u => u.DisplayName);
        string? NameOf(string? userId) => userId is not null ? users.GetValueOrDefault(userId) : null;

        var componentNames = components.ToDictionary(c => c.Id, c => c.Name);
        var projectsById = projects.ToDictionary(p => p.Id);

        var files = new List<ExportFile>();
        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var exportedDocuments = documents
            .OrderBy(d => d.Date)
            .ThenBy(d => d.UploadedAt)
            .Select(d =>
            {
                string? zipPath = null;
                if (d.StorageKind == DocumentStorageKind.Blob && d.BlobPath is { } blobPath)
                {
                    var project = d.ProjectId is not null ? projectsById.GetValueOrDefault(d.ProjectId) : null;
                    zipPath = Unique(PathFor(d, project), usedPaths);
                    files.Add(new ExportFile(d.Id, zipPath, blobPath));
                }

                return new ExportedDocument(
                    d.Id,
                    d.ProjectId,
                    d.Title,
                    d.FileName,
                    d.ContentType,
                    d.SizeBytes,
                    d.Category,
                    d.Date,
                    d.StorageKind,
                    zipPath,
                    d.DriveWebViewLink,
                    FileMissing: false,
                    NameOf(d.UploadedByUserId),
                    d.UploadedAt);
            })
            .ToList();

        var data = new PropertyExport(
            FormatVersion,
            DateTimeOffset.UtcNow,
            NameOf(exportedByUserId),
            new ExportedProperty(
                property.Id,
                property.Nickname,
                property.Address,
                property.Address2,
                property.PostalCode,
                property.City,
                property.Country,
                property.PropertyDesignation,
                property.YearBuilt,
                property.Type,
                property.Latitude,
                property.Longitude,
                property.PurchaseDate,
                property.PurchasePrice,
                property.IsDemo,
                property.ComponentsCustomized,
                (property.MemberUserIds ?? []).Select(NameOf).OfType<string>().ToList(),
                property.GoogleDriveFolderUrl,
                property.CreatedAt),
            components
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(c => new ExportedComponent(c.Id, c.Name, c.RecommendedIntervalMonths))
                .ToList(),
            projects
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ExportedProject(
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Notes,
                    p.WorkType,
                    p.ComponentId,
                    componentNames.GetValueOrDefault(p.ComponentId),
                    p.Status,
                    p.Priority,
                    p.IsUrgent,
                    p.ExcludeFromMaintenanceSchedule,
                    p.PlannedStartDate,
                    p.ActualStartDate,
                    p.CompletedDate,
                    p.EstimatedDurationDays,
                    p.EstimatedCost,
                    p.ActualCost,
                    p.EstimatedValueIncrease,
                    p.ExpectedLifespanYears,
                    p.EnergyEfficiencyGainPercent,
                    p.Contractor is { } c
                        ? new ExportedContractor(c.Name, c.Phone, c.Email, c.Website, c.QuotedPrice, c.QuotedDate, c.Notes)
                        : null,
                    p.Costs
                        .OrderBy(x => x.DateIncurred)
                        .Select(x => new ExportedCost(x.Id, x.Type, x.Description, x.Amount, x.DateIncurred, x.IsBudgeted))
                        .ToList(),
                    // Nullable on documents that predate milestones.
                    (p.Milestones ?? [])
                        .Select(m => new ExportedMilestone(m.Id, m.Description, m.PlannedDate, m.CompletedDate))
                        .ToList(),
                    NameOf(p.CreatedByUserId),
                    p.CreatedAt))
                .ToList(),
            valuations
                .OrderBy(v => v.Date)
                .Select(v => new ExportedValuation(v.Id, v.Date, v.Value, v.Source, v.Notes, NameOf(v.CreatedByUserId), v.CreatedAt))
                .ToList(),
            budgets
                .OrderBy(b => b.Year)
                .Select(b => new ExportedBudget(b.Year, b.MaintenanceBudget, b.RenovationBudget, b.InvestmentBudget, b.PurchaseBudget))
                .ToList(),
            exportedDocuments);

        return new PropertyExportBundle(data, files);
    }

    /// <summary>
    /// Mirrors the Drive folder tree <see cref="DriveFolderResolver"/> builds, so an exported Blob
    /// property and a Drive folder look the same to the person opening them.
    /// </summary>
    private static string PathFor(Document document, Project? project)
    {
        var folder = project is null
            ? $"{DocumentsFolder}/{DriveFolderResolver.GeneralFolderName}"
            : $"{DocumentsFolder}/{DriveFolderResolver.ProjectsFolderName}/{SafeName(DriveFolderResolver.FolderNameFor(project))}";

        var extension = Path.GetExtension(document.FileName);
        var label = string.IsNullOrWhiteSpace(document.Title)
            ? Path.GetFileNameWithoutExtension(document.FileName)
            : document.Title;
        if (extension.Length > 0 && label.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            label = label[..^extension.Length];
        }

        return $"{folder}/{SafeName($"{document.Date:yyyy-MM-dd} {label}")}{extension}";
    }

    /// <summary>
    /// Characters Windows refuses in a file name — the zip gets opened on any OS, not just the
    /// Linux one it's built on, where only '/' would matter.
    /// </summary>
    private static readonly char[] UnsafeChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static string SafeName(string name)
    {
        var cleaned = new string(name.Select(ch => char.IsControl(ch) || UnsafeChars.Contains(ch) ? '_' : ch).ToArray());
        cleaned = cleaned.Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "namnlös" : cleaned;
    }

    private static string Unique(string path, HashSet<string> used)
    {
        var extension = Path.GetExtension(path);
        var stem = path[..^extension.Length];
        var candidate = path;
        for (var n = 2; !used.Add(candidate); n++)
        {
            candidate = $"{stem} ({n}){extension}";
        }

        return candidate;
    }
}
