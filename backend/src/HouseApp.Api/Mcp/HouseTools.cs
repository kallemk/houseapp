using System.ComponentModel;
using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HouseApp.Api.Mcp;

/// <summary>
/// What Claude can do with a user's houses. Three rules shape all of it:
///
/// - **Member-only, not demo.** Every tool checks strict membership (<c>PropertyAccess.IsMember</c>),
///   never <c>CanAccessPropertyAsync</c>. The demo is editable by anyone on the internet, and whatever
///   is written there would land in Claude's context — a ready-made prompt-injection path into the
///   user's own houses. A property you're not in answers exactly like one that doesn't exist.
/// - **Nothing is deleted.** Not projects, costs, milestones nor documents. A misread instruction in a
///   chat should cost an edit, not data; deleting stays in the app.
/// - **Writes go through the same services as the REST API** (<see cref="ProjectService"/>,
///   <see cref="DocumentService"/>), so limits, Drive folders and every other rule apply unchanged.
///
/// Descriptions are written for the model. Output carries the Swedish label next to each English
/// enum value, because that's what the user sees in the app and will talk about in the chat.
/// </summary>
[McpServerToolType]
public class HouseTools(
    AppDbContext db,
    ProjectService projects,
    DocumentService documents,
    IHttpContextAccessor httpContextAccessor)
{
    private const string WorkTypeHelp =
        "Maintenance = preserve or replace what exists (underhåll). " +
        "Renovation = improve what exists (renovering). " +
        "Investment = add something new to the property (nyinvestering). " +
        "Purchase = buy movable property or equipment (inköp). " +
        "Choose carefully: the type decides which totals and budget line the money counts toward.";

    private HttpContext Http => httpContextAccessor.HttpContext!;

    private string UserId => Http.User.CurrentUserId();

    // --- Reading ------------------------------------------------------------------------------------

    [McpServerTool(Name = "list_properties", ReadOnly = true, Idempotent = true)]
    [Description("Lists the user's own houses in HusTracker. Start here: every other tool needs a propertyId.")]
    public async Task<object> ListProperties()
    {
        var all = await db.Properties.ToListAsync();
        return all
            .Where(p => !p.IsDemo && PropertyAccess.IsMember(p, UserId))
            .Select(p => new
            {
                p.Id,
                p.Nickname,
                p.Address,
                p.City,
                p.YearBuilt,
                Type = p.Type is { } t ? Labels.PropertyTypes[t] : null,
                p.PurchaseDate,
                p.PurchasePrice,
            })
            .ToList();
    }

    [McpServerTool(Name = "list_components", ReadOnly = true, Idempotent = true)]
    [Description("Lists the parts of the house (roof, facade, plumbing, ...) that projects belong to, with their recommended maintenance interval. Every project needs one of these ids as componentId.")]
    public async Task<object> ListComponents([Description("From list_properties.")] string propertyId)
    {
        var property = await MemberPropertyAsync(propertyId);
        return (await db.GetEffectiveComponentsAsync(property))
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.RecommendedIntervalMonths })
            .ToList();
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, Idempotent = true)]
    [Description("Lists a house's projects, newest first, optionally only those with a given status. Use get_project for costs, milestones and contractor.")]
    public async Task<object> ListProjects(
        [Description("From list_properties.")] string propertyId,
        [Description("Only projects with this status.")] ProjectStatus? status = null)
    {
        await MemberPropertyAsync(propertyId);
        var names = await ComponentNamesAsync(propertyId);
        return (await projects.ListAsync(propertyId))
            .Where(p => status is null || p.Status == status)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.WorkType,
                WorkTypeLabel = Labels.WorkTypes[p.WorkType],
                p.Status,
                StatusLabel = Labels.Statuses[p.Status],
                Component = names.GetValueOrDefault(p.ComponentId),
                p.PlannedStartDate,
                p.CompletedDate,
                p.EstimatedCost,
                p.ActualCost,
                p.IsUrgent,
            })
            .ToList();
    }

    [McpServerTool(Name = "get_project", ReadOnly = true, Idempotent = true)]
    [Description("Gets one project in full: description, notes, dates, estimate vs. actual cost, cost rows, milestones and contractor.")]
    public async Task<object> GetProject(string propertyId, string projectId)
    {
        await MemberPropertyAsync(propertyId);
        var project = await ProjectAsync(propertyId, projectId);
        return await DescribeAsync(project);
    }

    [McpServerTool(Name = "get_maintenance_schedule", ReadOnly = true, Idempotent = true)]
    [Description("When each part of the house is next due for maintenance, most urgent first. 'Baseline: YearBuilt' means nothing has been logged yet and the build year is assumed — a starting point, not a record of work done.")]
    public async Task<object> GetMaintenanceSchedule(string propertyId)
    {
        var property = await MemberPropertyAsync(propertyId);
        return await MaintenanceScheduleCalculator.ComputeAsync(db, property, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [McpServerTool(Name = "get_budget", ReadOnly = true, Idempotent = true)]
    [Description("A year's budget against actual spending, per work type. Spending is summed from project cost rows by the date each cost was incurred.")]
    public async Task<object> GetBudget(
        string propertyId,
        [Description("Defaults to the current year.")] int? year = null)
    {
        await MemberPropertyAsync(propertyId);
        var y = year ?? DateTime.UtcNow.Year;
        var budget = (await db.Budgets.Where(b => b.PropertyId == propertyId).ToListAsync()).FirstOrDefault(b => b.Year == y);
        var all = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();
        var dto = BudgetCalculator.Build(propertyId, y, budget, all);
        return new
        {
            dto.Year,
            HasBudget = budget is not null,
            Lines = dto.Lines.Select(l => new { l.WorkType, Label = Labels.WorkTypes[l.WorkType], l.Budgeted, l.Spent }),
            dto.TotalBudgeted,
            dto.TotalSpent,
        };
    }

    [McpServerTool(Name = "list_documents", ReadOnly = true, Idempotent = true)]
    [Description("Lists a house's documents (receipts, quotes, warranties, photos ...), optionally only those attached to one project. Metadata only — file contents aren't available here.")]
    public async Task<object> ListDocuments(
        string propertyId,
        [Description("Only documents attached to this project.")] string? projectId = null)
    {
        await MemberPropertyAsync(propertyId);
        return (await documents.ListAsync(propertyId))
            .Where(d => projectId is null || d.ProjectId == projectId)
            .Select(d => new
            {
                d.Id,
                Title = d.Title ?? d.FileName,
                d.FileName,
                d.Date,
                d.Category,
                CategoryLabel = Labels.DocumentCategories[d.Category],
                d.ProjectId,
                Storage = Labels.StorageKinds[d.StorageKind],
            })
            .ToList();
    }

    // --- Projects -----------------------------------------------------------------------------------

    [McpServerTool(Name = "create_project", Destructive = false, Idempotent = false)]
    [Description("Creates a project — planned, ongoing or finished work on the house. " + WorkTypeHelp)]
    public async Task<object> CreateProject(
        string propertyId,
        string name,
        [Description(WorkTypeHelp)] WorkType workType,
        [Description("Which part of the house, from list_components.")] string componentId,
        ProjectStatus status = ProjectStatus.Planned,
        ProjectPriority priority = ProjectPriority.Medium,
        [Description("Rough expected cost in SEK.")] decimal estimatedCost = 0,
        string? description = null,
        string? notes = null,
        DateOnly? plannedStartDate = null,
        DateOnly? completedDate = null,
        bool isUrgent = false)
    {
        await MemberPropertyAsync(propertyId);
        await EnsureComponentAsync(propertyId, componentId);

        var project = new Project
        {
            PropertyId = propertyId,
            Name = Text(name, nameof(name), Limits.ShortText),
            ComponentId = componentId,
            WorkType = workType,
            Status = status,
            Priority = priority,
            EstimatedCost = estimatedCost,
            Description = OptionalText(description, Limits.LongText),
            Notes = OptionalText(notes, Limits.LongText),
            PlannedStartDate = plannedStartDate,
            CompletedDate = completedDate,
            IsUrgent = isUrgent,
            CreatedByUserId = UserId,
        };

        await Limited(() => projects.CreateAsync(project));
        return await DescribeAsync(project);
    }

    [McpServerTool(Name = "update_project", Destructive = false, Idempotent = true)]
    [Description("Changes a project. Only the fields you pass change; everything else — including cost rows and milestones — is left as it is. Use add_project_cost / add_project_milestone / update_milestone for those.")]
    public async Task<object> UpdateProject(
        string propertyId,
        string projectId,
        string? name = null,
        [Description(WorkTypeHelp)] WorkType? workType = null,
        [Description("From list_components.")] string? componentId = null,
        ProjectStatus? status = null,
        ProjectPriority? priority = null,
        decimal? estimatedCost = null,
        string? description = null,
        string? notes = null,
        DateOnly? plannedStartDate = null,
        DateOnly? actualStartDate = null,
        DateOnly? completedDate = null,
        bool? isUrgent = null,
        [Description("Contractor (company) name. Pass with the other contractor fields to set or change it.")] string? contractorName = null,
        string? contractorPhone = null,
        string? contractorEmail = null,
        [Description("Price quoted by the contractor, SEK.")] decimal? contractorQuotedPrice = null)
    {
        await MemberPropertyAsync(propertyId);
        var project = await ProjectAsync(propertyId, projectId);
        if (componentId is not null)
        {
            await EnsureComponentAsync(propertyId, componentId);
        }

        var newName = name is null ? null : Text(name, nameof(name), Limits.ShortText);
        await Limited(() => projects.UpdateAsync(project, p =>
        {
            if (newName is not null) p.Name = newName;
            if (workType is { } w) p.WorkType = w;
            if (componentId is not null) p.ComponentId = componentId;
            if (status is { } s) p.Status = s;
            if (priority is { } pr) p.Priority = pr;
            if (estimatedCost is { } e) p.EstimatedCost = e;
            if (description is not null) p.Description = OptionalText(description, Limits.LongText);
            if (notes is not null) p.Notes = OptionalText(notes, Limits.LongText);
            if (plannedStartDate is not null) p.PlannedStartDate = plannedStartDate;
            if (actualStartDate is not null) p.ActualStartDate = actualStartDate;
            if (completedDate is not null) p.CompletedDate = completedDate;
            if (isUrgent is { } u) p.IsUrgent = u;

            if (contractorName is not null || contractorPhone is not null || contractorEmail is not null || contractorQuotedPrice is not null)
            {
                p.Contractor ??= new ContractorInfo { Name = "" };
                if (contractorName is not null) p.Contractor.Name = Text(contractorName, nameof(contractorName), Limits.ShortText);
                if (contractorPhone is not null) p.Contractor.Phone = OptionalText(contractorPhone, Limits.ShortText);
                if (contractorEmail is not null) p.Contractor.Email = OptionalText(contractorEmail, Limits.ShortText);
                if (contractorQuotedPrice is { } q) p.Contractor.QuotedPrice = q;
                if (string.IsNullOrWhiteSpace(p.Contractor.Name))
                {
                    throw new McpException("A contractor needs a name.");
                }
            }
        }));

        return await DescribeAsync(project);
    }

    [McpServerTool(Name = "add_project_cost", Destructive = false, Idempotent = false)]
    [Description("Records money actually spent on a project — an invoice, a receipt, a material purchase. It counts toward that year's budget by dateIncurred. Calling twice records it twice.")]
    public async Task<object> AddProjectCost(
        string propertyId,
        string projectId,
        CostType type,
        [Description("Amount in SEK.")] decimal amount,
        DateOnly dateIncurred,
        string? description = null,
        [Description("Whether the cost was planned in the budget.")] bool isBudgeted = false)
    {
        await MemberPropertyAsync(propertyId);
        var project = await ProjectAsync(propertyId, projectId);
        await Limited(() => projects.UpdateAsync(project, p => p.Costs.Add(new ProjectCost
        {
            Type = type,
            Amount = amount,
            DateIncurred = dateIncurred,
            Description = OptionalText(description, Limits.ShortText),
            IsBudgeted = isBudgeted,
        })));
        return await DescribeAsync(project);
    }

    [McpServerTool(Name = "add_project_milestone", Destructive = false, Idempotent = false)]
    [Description("Adds a step to a project's plan, e.g. 'Riva gamla taket'. Mark it done later with update_milestone.")]
    public async Task<object> AddProjectMilestone(
        string propertyId,
        string projectId,
        string description,
        DateOnly? plannedDate = null)
    {
        await MemberPropertyAsync(propertyId);
        var project = await ProjectAsync(propertyId, projectId);
        var text = Text(description, nameof(description), Limits.ShortText);
        await Limited(() => projects.UpdateAsync(project, p =>
        {
            p.Milestones ??= [];
            p.Milestones.Add(new ProjectMilestone { Description = text, PlannedDate = plannedDate });
        }));
        return await DescribeAsync(project);
    }

    [McpServerTool(Name = "update_milestone", Destructive = false, Idempotent = true)]
    [Description("Changes a milestone's planned date or marks it completed. Milestone ids come from get_project, and change if the project is saved in the app — fetch the project again if one isn't found.")]
    public async Task<object> UpdateMilestone(
        string propertyId,
        string projectId,
        string milestoneId,
        DateOnly? plannedDate = null,
        [Description("Set to mark the milestone done.")] DateOnly? completedDate = null,
        string? description = null)
    {
        await MemberPropertyAsync(propertyId);
        var project = await ProjectAsync(propertyId, projectId);
        var milestone = (project.Milestones ?? []).FirstOrDefault(m => m.Id == milestoneId)
            ?? throw new McpException("No such milestone on this project. Fetch it again with get_project.");
        var text = description is null ? null : Text(description, nameof(description), Limits.ShortText);

        await Limited(() => projects.UpdateAsync(project, _ =>
        {
            if (plannedDate is not null) milestone.PlannedDate = plannedDate;
            if (completedDate is not null) milestone.CompletedDate = completedDate;
            if (text is not null) milestone.Description = text;
        }));
        return await DescribeAsync(project);
    }

    // --- Documents ----------------------------------------------------------------------------------

    [McpServerTool(Name = "update_document", Destructive = false, Idempotent = true)]
    [Description("Corrects a document's title, date or category. Only what you pass changes. The stored file isn't touched.")]
    public async Task<object> UpdateDocument(
        string propertyId,
        string documentId,
        string? title = null,
        DateOnly? date = null,
        DocumentCategory? category = null)
    {
        await MemberPropertyAsync(propertyId);
        var document = await documents.FindAsync(propertyId, documentId)
            ?? throw new McpException("No such document in this house.");

        // Documents predating titles have none; a title is required from here on, as in the app.
        var newTitle = title is null ? document.Title : Text(title, nameof(title), Limits.ShortText);
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new McpException("This document has no title yet — pass one.");
        }

        await documents.UpdateMetadataAsync(document, newTitle, date ?? document.Date, category ?? document.Category);
        return DocumentService.ToDto(document);
    }

    [McpServerTool(Name = "set_document_project", Destructive = false, Idempotent = true)]
    [Description("Attaches a document to a project, or detaches it (projectId omitted). If the house stores documents in Google Drive, the file is moved into the project's folder to match.")]
    public async Task<object> SetDocumentProject(
        string propertyId,
        string documentId,
        [Description("Omit to detach from any project.")] string? projectId = null)
    {
        var property = await MemberPropertyAsync(propertyId);
        var document = await documents.FindAsync(propertyId, documentId)
            ?? throw new McpException("No such document in this house.");

        try
        {
            if (!await documents.SetProjectAsync(property, document, projectId))
            {
                throw new McpException("No such project in this house.");
            }
        }
        catch (DriveConnectionExpiredException)
        {
            throw new McpException(
                "The house's Google Drive connection has expired, so the file can't be moved. " +
                "Ask the user to renew it on the Documents page in HusTracker, then try again.");
        }

        return DocumentService.ToDto(document);
    }

    [McpServerTool(Name = "create_upload_link", ReadOnly = true, Idempotent = true)]
    [Description(
        "Files can't be sent through this connection — not even ones the user attached in this chat, because only their text " +
        "and images reach you, not the original file. Instead, this returns a link that opens HusTracker's upload dialog for " +
        "the project (or the house's documents page), with your suggested title, category and date filled in. Give the link " +
        "to the user; they drop the original file there.")]
    public async Task<object> CreateUploadLink(
        string propertyId,
        [Description("Attach the upload to this project. Omit for a document that belongs to the house in general.")] string? projectId = null,
        [Description("Suggested title, e.g. 'Offert Takfirman AB'.")] string? title = null,
        DocumentCategory? category = null,
        [Description("The date the document is from, e.g. the invoice date.")] DateOnly? date = null)
    {
        await MemberPropertyAsync(propertyId);
        if (projectId is not null)
        {
            await ProjectAsync(propertyId, projectId);
        }

        var page = projectId is null
            ? $"/properties/{propertyId}/documents"
            : $"/properties/{propertyId}/projects/{projectId}";
        var query = new List<string> { "upload=1" };
        if (!string.IsNullOrWhiteSpace(title)) query.Add($"title={Uri.EscapeDataString(title.Trim())}");
        if (category is { } c) query.Add($"category={c}");
        if (date is { } d) query.Add($"date={d:yyyy-MM-dd}");

        return new { Url = $"{McpOAuth.Origin(Http.Request)}{page}?{string.Join("&", query)}" };
    }

    // --- Helpers ------------------------------------------------------------------------------------

    /// <summary>Strict membership — see the class summary for why the demo is excluded.</summary>
    private async Task<Property> MemberPropertyAsync(string propertyId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        if (property is null || property.IsDemo || !PropertyAccess.IsMember(property, UserId))
        {
            throw new McpException("No such house. Use list_properties for valid ids.");
        }

        return property;
    }

    private async Task<Project> ProjectAsync(string propertyId, string projectId) =>
        await projects.FindAsync(propertyId, projectId)
            ?? throw new McpException("No such project in this house. Use list_projects for valid ids.");

    private async Task EnsureComponentAsync(string propertyId, string componentId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        if ((await db.GetEffectiveComponentsAsync(property!)).All(c => c.Id != componentId))
        {
            throw new McpException("Unknown componentId. Use list_components for valid ids.");
        }
    }

    private async Task<Dictionary<string, string>> ComponentNamesAsync(string propertyId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        return (await db.GetEffectiveComponentsAsync(property!)).ToDictionary(c => c.Id, c => c.Name);
    }

    private async Task<object> DescribeAsync(Project p)
    {
        var names = await ComponentNamesAsync(p.PropertyId);
        var dto = ProjectService.ToDto(p);
        return new
        {
            dto.Id,
            dto.Name,
            dto.Description,
            dto.Notes,
            dto.WorkType,
            WorkTypeLabel = Labels.WorkTypes[dto.WorkType],
            dto.Status,
            StatusLabel = Labels.Statuses[dto.Status],
            dto.Priority,
            dto.IsUrgent,
            dto.ComponentId,
            Component = names.GetValueOrDefault(dto.ComponentId),
            dto.PlannedStartDate,
            dto.ActualStartDate,
            dto.CompletedDate,
            dto.EstimatedCost,
            dto.ActualCost,
            dto.Contractor,
            Costs = dto.Costs.Select(c => new { c.Type, TypeLabel = Labels.CostTypes[c.Type], c.Description, c.Amount, c.DateIncurred, c.IsBudgeted }),
            dto.Milestones,
        };
    }

    /// <summary>Limits from the shared services come back to Claude as a tool error, in the user's language.</summary>
    private static async Task Limited(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (LimitReachedException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    // The tools don't go through [ApiController] model validation, so lengths are checked here.
    private static string Text(string value, string field, int max)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new McpException($"{field} can't be empty.");
        }

        return trimmed.Length <= max ? trimmed : throw new McpException($"{field} is too long (max {max} characters).");
    }

    private static string? OptionalText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : throw new McpException($"Text is too long (max {max} characters).");
    }
}
