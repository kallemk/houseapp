namespace HouseApp.Api.Services;

/// <summary>A comment, with enough to say who replied and when.</summary>
public record GitHubComment(string Body, string Author, DateTimeOffset CreatedAt);

/// <summary>One issue as the app cares about it — GitHub returns a great deal more.</summary>
public record GitHubIssue(
    int Number,
    string Title,
    string Body,
    IReadOnlyList<string> Labels,
    bool IsOpen,
    int CommentCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// Files user suggestions into the repository's issues and reads them back.
///
/// An interface for the same reason <see cref="IGoogleDriveService"/> is one: the tests substitute a
/// fake and never touch GitHub, so the visibility rules — which are the part that actually matters —
/// stay under test.
/// </summary>
public interface IGitHubIssueService
{
    /// <summary>False when no token is configured; the endpoints answer 503 rather than failing oddly.</summary>
    bool IsConfigured { get; }

    Task<GitHubIssue> CreateIssueAsync(
        string title,
        string body,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every issue carrying <paramref name="label"/>. Deliberately a filtered read rather than "all
    /// issues": the app must never see ordinary development issues, and filtering at the source is
    /// what guarantees that rather than a condition somewhere further down that could be edited out.
    /// </summary>
    Task<IReadOnlyList<GitHubIssue>> ListIssuesAsync(string label, CancellationToken cancellationToken = default);

    /// <summary>The most recent comment, used to show the owner's reply. Null when there are none.</summary>
    Task<GitHubComment?> GetLatestCommentAsync(int issueNumber, CancellationToken cancellationToken = default);
}
