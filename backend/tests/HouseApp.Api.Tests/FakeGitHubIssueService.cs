using System.Collections.Concurrent;
using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

/// <summary>
/// Stands in for GitHub, in the same spirit as the Blob, Google and Drive fakes: only the outbound
/// calls are faked, so the label contract and the visibility rules — the parts that decide whether a
/// private development issue can leak into the app — are exercised for real.
/// </summary>
public class FakeGitHubIssueService : IGitHubIssueService
{
    private int _nextNumber = 1;

    public ConcurrentDictionary<int, GitHubIssue> Issues { get; } = new();
    public ConcurrentDictionary<int, string> Comments { get; } = new();

    public bool IsConfigured { get; set; } = true;

    public Task<GitHubIssue> CreateIssueAsync(
        string title,
        string body,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken = default)
    {
        var number = Interlocked.Increment(ref _nextNumber);
        var issue = new GitHubIssue(number, title, body, labels.ToList(), IsOpen: true, CommentCount: 0, DateTimeOffset.UtcNow);
        Issues[number] = issue;
        return Task.FromResult(issue);
    }

    /// <summary>
    /// Filters by label exactly as the real API does. That's the point: a test that seeds an
    /// unlabelled issue must find it genuinely unreachable, not merely filtered out later.
    /// </summary>
    public Task<IReadOnlyList<GitHubIssue>> ListIssuesAsync(string label, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GitHubIssue>>(Issues.Values.Where(i => i.Labels.Contains(label)).ToList());

    public Task<string?> GetLatestCommentAsync(int issueNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comments.GetValueOrDefault(issueNumber));

    // --- helpers for arranging test state -------------------------------------------------------

    /// <summary>Seeds an issue directly, for the cases the app would never create itself.</summary>
    public GitHubIssue Seed(string title, string body, string[] labels, bool isOpen = true)
    {
        var number = Interlocked.Increment(ref _nextNumber);
        var issue = new GitHubIssue(number, title, body, labels, isOpen, CommentCount: 0, DateTimeOffset.UtcNow);
        Issues[number] = issue;
        return issue;
    }

    public void AddLabel(int number, string label)
    {
        var issue = Issues[number];
        Issues[number] = issue with { Labels = [.. issue.Labels, label] };
    }

    public void AddComment(int number, string body)
    {
        Comments[number] = body;
        var issue = Issues[number];
        Issues[number] = issue with { CommentCount = issue.CommentCount + 1 };
    }

    public void Clear()
    {
        Issues.Clear();
        Comments.Clear();
    }
}
