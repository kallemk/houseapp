using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HouseApp.Api.Services;

/// <summary>
/// Talks to the GitHub REST API with a fine-grained personal access token scoped to this repository
/// alone (Issues: read and write). Issues are therefore authored by the token's owner — the real
/// submitter is named in the body, which FeedbackController writes.
/// </summary>
public class GitHubIssueService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubIssueService> logger) : IGitHubIssueService
{
    private string? Token => configuration["GitHub:Token"];
    private string Owner => configuration["GitHub:Owner"] ?? string.Empty;
    private string Repo => configuration["GitHub:Repo"] ?? string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Owner) && !string.IsNullOrWhiteSpace(Repo);

    public async Task<GitHubIssue> CreateIssueAsync(
        string title,
        string body,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            $"repos/{Owner}/{Repo}/issues",
            new { title, body, labels },
            cancellationToken);

        await ThrowIfFailedAsync(response, "create an issue", cancellationToken);
        var created = await response.Content.ReadFromJsonAsync<IssueResponse>(cancellationToken);
        return ToIssue(created!);
    }

    public async Task<IReadOnlyList<GitHubIssue>> ListIssuesAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        // state=all so a closed suggestion still shows with its outcome rather than vanishing.
        var response = await client.GetAsync(
            $"repos/{Owner}/{Repo}/issues?labels={Uri.EscapeDataString(label)}&state=all&per_page=100",
            cancellationToken);

        await ThrowIfFailedAsync(response, "list issues", cancellationToken);
        var issues = await response.Content.ReadFromJsonAsync<List<IssueResponse>>(cancellationToken) ?? [];

        // This endpoint returns pull requests as well as issues — they're the same object to GitHub,
        // distinguishable only by the presence of a pull_request field. Without this a PR carrying
        // the label would surface to users as a suggestion.
        return issues.Where(i => i.PullRequest is null).Select(ToIssue).ToList();
    }

    public async Task<GitHubComment?> GetLatestCommentAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var response = await client.GetAsync(
            $"repos/{Owner}/{Repo}/issues/{issueNumber}/comments?per_page=100",
            cancellationToken);

        await ThrowIfFailedAsync(response, "read comments", cancellationToken);
        var comments = await response.Content.ReadFromJsonAsync<List<CommentResponse>>(cancellationToken) ?? [];
        var latest = comments.LastOrDefault();
        return latest is null
            ? null
            : new GitHubComment(latest.Body, latest.User?.Login ?? "okänd", latest.CreatedAt);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(nameof(GitHubIssueService));
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        // GitHub rejects requests without one.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HusTracker");
        return client;
    }

    private async Task ThrowIfFailedAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The body can echo back the issue text, so only the status is logged.
        logger.LogWarning("GitHub returned {Status} trying to {What}.", response.StatusCode, what);
        await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"GitHub request failed with {response.StatusCode}.");
    }

    private static GitHubIssue ToIssue(IssueResponse issue) =>
        new(
            issue.Number,
            issue.Title,
            issue.Body ?? string.Empty,
            issue.Labels.Select(l => l.Name).ToList(),
            string.Equals(issue.State, "open", StringComparison.OrdinalIgnoreCase),
            issue.Comments,
            issue.CreatedAt);

    private record IssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("comments")] int Comments,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("labels")] List<LabelResponse> Labels,
        [property: JsonPropertyName("pull_request")] JsonElement? PullRequest);

    private record LabelResponse([property: JsonPropertyName("name")] string Name);

    private record CommentResponse(
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("user")] UserResponse? User);

    private record UserResponse([property: JsonPropertyName("login")] string Login);
}
