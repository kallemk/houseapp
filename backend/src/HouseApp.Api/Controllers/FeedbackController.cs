using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Feedback;
using HouseApp.Api.Extensions;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace HouseApp.Api.Controllers;

/// <summary>
/// User suggestions, stored as issues in the app's own GitHub repository.
///
/// **The `feedback` label is the whole safety mechanism.** The app only ever asks GitHub for issues
/// carrying it, so ordinary development issues are not filtered out — they are never read. That's a
/// deliberately stronger guarantee than a condition further down the pipeline, which could be edited
/// away without anyone noticing until private planning notes appeared in the app.
///
/// Publishing is opt-in on top of that: a suggestion is only visible to *other* users once the owner
/// adds `publik`. Until then its submitter can see it (so they know it arrived) and so can admins (so
/// they can triage), and nobody else can.
/// </summary>
[ApiController]
[Route("api/feedback")]
[Authorize]
public class FeedbackController(
    AppDbContext db,
    IGitHubIssueService gitHub,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<FeedbackController> logger) : ControllerBase
{
    /// <summary>Applied to everything the app files. Nothing without it is ever read back.</summary>
    private const string FeedbackLabel = "feedback";

    /// <summary>Added by the owner in GitHub. Without it, only the submitter and admins see it.</summary>
    private const string PublishedLabel = "publik";

    private const string StatusLabelPrefix = "status:";

    /// <summary>
    /// Every user hitting the page would otherwise be a pair of GitHub calls. The list changes rarely
    /// and a stale minute costs nothing, whereas a rate-limited API in the middle of a page load
    /// costs the whole feature.
    /// </summary>
    private const int DefaultCacheSeconds = 120;

    private const string CacheKey = "feedback:issues";

    /// <summary>Zero disables caching entirely — the tests set that, so seeding is immediately visible.</summary>
    private TimeSpan CacheFor =>
        TimeSpan.FromSeconds(int.TryParse(configuration["Feedback:CacheSeconds"], out var seconds)
            ? seconds
            : DefaultCacheSeconds);

    /// <summary>
    /// Anyone can sign up now, and this endpoint writes into a private repository. A generous daily
    /// cap keeps an enthusiastic user working while stopping an automated one filling the backlog.
    /// </summary>
    private const int MaxPerUserPerDay = 5;

    [HttpGet]
    public async Task<ActionResult<List<FeedbackItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (!gitHub.IsConfigured)
        {
            return Unavailable();
        }

        var userId = User.CurrentUserId();
        var isAdmin = await IsAdminAsync(userId);

        var issues = await GetIssuesAsync(cancellationToken);
        var visible = issues
            .Where(i => i.Labels.Contains(PublishedLabel) || isAdmin || IsSubmittedBy(i.Body, userId))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        var items = new List<FeedbackItemDto>();
        foreach (var issue in visible)
        {
            // Only for issues that actually have comments — the list endpoint tells us the count, so
            // the common case costs no extra call at all.
            var reply = issue.CommentCount > 0
                ? await gitHub.GetLatestCommentAsync(issue.Number, cancellationToken)
                : null;

            items.Add(new FeedbackItemDto(
                issue.Number,
                issue.Title,
                StripMarker(issue.Body),
                StatusOf(issue),
                reply,
                IsSubmittedBy(issue.Body, userId),
                issue.Labels.Contains(PublishedLabel),
                issue.CreatedAt));
        }

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<FeedbackItemDto>> Create(
        CreateFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!gitHub.IsConfigured)
        {
            return Unavailable();
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "A title and a description are required." });
        }

        var userId = User.CurrentUserId();
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var existing = await GetIssuesAsync(cancellationToken);
        var since = DateTimeOffset.UtcNow.AddDays(-1);
        if (existing.Count(i => IsSubmittedBy(i.Body, userId) && i.CreatedAt >= since) >= MaxPerUserPerDay)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Du har skickat in många förslag idag. Försök igen imorgon.",
            });
        }

        // The submitter's *name and id*, deliberately never their email — the whole point of the
        // marker is that GitHub can identify who to look up in the app without holding contact
        // details of its own.
        var body = $"""
            {request.Body.Trim()}

            ---
            Inskickat av {user.DisplayName} ({userId}) via HusTracker.
            {MarkerFor(userId)}
            """;

        var issue = await gitHub.CreateIssueAsync(request.Title.Trim(), body, [FeedbackLabel], cancellationToken);
        cache.Remove(CacheKey);
        logger.LogInformation("Filed suggestion #{Number} from {UserId}.", issue.Number, userId);

        return Ok(new FeedbackItemDto(
            issue.Number,
            issue.Title,
            request.Body.Trim(),
            FeedbackStatus.New,
            Reply: null,
            IsMine: true,
            IsPublished: false,
            issue.CreatedAt));
    }

    private async Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(CancellationToken cancellationToken)
    {
        var cacheFor = CacheFor;
        if (cacheFor > TimeSpan.Zero
            && cache.TryGetValue(CacheKey, out IReadOnlyList<GitHubIssue>? cached)
            && cached is not null)
        {
            return cached;
        }

        var issues = await gitHub.ListIssuesAsync(FeedbackLabel, cancellationToken);
        if (cacheFor > TimeSpan.Zero)
        {
            cache.Set(CacheKey, issues, cacheFor);
        }

        return issues;
    }

    private async Task<bool> IsAdminAsync(string userId) =>
        (await db.Users.FindAsync(userId))?.IsAdmin == true;

    /// <summary>
    /// An HTML comment, so it's invisible when the issue is read on GitHub but trivially matchable
    /// here. This is what makes "show me my own" and the daily cap work without a container of our
    /// own — see CLAUDE.md for why that trade was taken.
    /// </summary>
    private static string MarkerFor(string userId) => $"<!-- houseapp:submitter={userId} -->";

    private static bool IsSubmittedBy(string body, string userId) =>
        body.Contains(MarkerFor(userId), StringComparison.Ordinal);

    /// <summary>The marker is plumbing; nobody reading their own suggestion in the app needs to see it.</summary>
    private static string StripMarker(string body)
    {
        var index = body.IndexOf("<!-- houseapp:submitter=", StringComparison.Ordinal);
        return index < 0 ? body : body[..index].TrimEnd();
    }

    private static FeedbackStatus StatusOf(GitHubIssue issue)
    {
        var status = issue.Labels
            .FirstOrDefault(l => l.StartsWith(StatusLabelPrefix, StringComparison.OrdinalIgnoreCase))
            ?[StatusLabelPrefix.Length..]
            .Trim();

        return status?.ToLowerInvariant() switch
        {
            "planerad" => FeedbackStatus.Planned,
            "pågår" => FeedbackStatus.InProgress,
            "klar" => FeedbackStatus.Done,
            "avvisad" => FeedbackStatus.Declined,
            // No status label: fall back to the issue's own state, so an unlabelled but closed
            // suggestion still reads as finished rather than as never looked at.
            _ => issue.IsOpen ? FeedbackStatus.New : FeedbackStatus.Done,
        };
    }

    private ObjectResult Unavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            message = "Förslagsfunktionen är inte konfigurerad på servern.",
        });
}
