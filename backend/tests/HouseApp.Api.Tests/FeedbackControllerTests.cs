using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Feedback;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Suggestions live as GitHub issues in the app's own repository, which also holds ordinary
/// development work. Most of what can go wrong here is a visibility mistake, so that's what these
/// cover — above all that an unlabelled issue is unreachable rather than merely hidden.
/// </summary>
public class FeedbackControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public FeedbackControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.GitHub.Clear();
        _factory.GitHub.IsConfigured = true;
    }

    private async Task<(HttpClient Client, string UserId)> CreateClientAsync(bool isAdmin = false)
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser { Email = email, DisplayName = "Feedback Tester", IsAdmin = isAdmin };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, userId);
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string title, string body = "Vore bra att ha.") =>
        client.PostAsJsonAsync("/api/feedback", new CreateFeedbackRequest(title, body));

    private static async Task<List<FeedbackItemDto>> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/feedback");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<FeedbackItemDto>>())!;
    }

    [Fact]
    public async Task Submitting_FilesAnIssueLabelledFeedback_WithTheSubmitterButNotTheirEmail()
    {
        var (client, userId) = await CreateClientAsync();

        var response = await SubmitAsync(client, "Mörkt läge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var issue = Assert.Single(_factory.GitHub.Issues.Values);
        Assert.Contains("feedback", issue.Labels);
        Assert.Contains(userId, issue.Body);
        Assert.Contains("Feedback Tester", issue.Body);
        // The whole point of choosing name-and-id: contact details stay inside the app.
        Assert.DoesNotContain("@example.com", issue.Body);
    }

    [Fact]
    public async Task AnIssueWithoutTheFeedbackLabel_IsNeverVisible_EvenToAnAdmin()
    {
        // The guard that keeps development issues out of the app. Admins see *more* feedback, not
        // more of the repository.
        _factory.GitHub.Seed("Intern planering", "Hemligt", labels: []);
        var (client, _) = await CreateClientAsync(isAdmin: true);

        Assert.Empty(await ListAsync(client));
    }

    [Fact]
    public async Task ASuggestionIsHiddenFromOthersUntilItIsPublished()
    {
        var (author, _) = await CreateClientAsync();
        await SubmitAsync(author, "Exportera till Excel");
        var number = _factory.GitHub.Issues.Keys.Single();

        var (stranger, _) = await CreateClientAsync();
        Assert.Empty(await ListAsync(stranger));

        // The submitter always sees their own, so they know it arrived.
        var mine = Assert.Single(await ListAsync(author));
        Assert.True(mine.IsMine);
        Assert.False(mine.IsPublished);

        _factory.GitHub.AddLabel(number, "publik");

        var published = Assert.Single(await ListAsync(stranger));
        Assert.True(published.IsPublished);
        Assert.False(published.IsMine);
    }

    [Fact]
    public async Task AnAdminSeesUnpublishedSuggestions_SoTheyCanTriage()
    {
        var (author, _) = await CreateClientAsync();
        await SubmitAsync(author, "Påminnelser via e-post");

        var (admin, _) = await CreateClientAsync(isAdmin: true);

        var item = Assert.Single(await ListAsync(admin));
        Assert.False(item.IsPublished);
        Assert.False(item.IsMine);
    }

    [Theory]
    [InlineData("status:planerad", FeedbackStatus.Planned)]
    [InlineData("status:pågår", FeedbackStatus.InProgress)]
    [InlineData("status:klar", FeedbackStatus.Done)]
    [InlineData("status:avvisad", FeedbackStatus.Declined)]
    public async Task AStatusLabelIsReportedAsTheStatus(string label, FeedbackStatus expected)
    {
        var (client, _) = await CreateClientAsync();
        await SubmitAsync(client, "Något");
        var number = _factory.GitHub.Issues.Keys.Single();
        _factory.GitHub.AddLabel(number, label);

        Assert.Equal(expected, (await ListAsync(client)).Single().Status);
    }

    [Fact]
    public async Task WithoutAStatusLabel_AClosedIssueStillReadsAsDone()
    {
        // Forgetting to label something should be harmless, not misleading.
        _factory.GitHub.Seed("Gammalt förslag", "Text", labels: ["feedback", "publik"], isOpen: false);
        var (client, _) = await CreateClientAsync();

        Assert.Equal(FeedbackStatus.Done, (await ListAsync(client)).Single().Status);
    }

    [Fact]
    public async Task TheLatestCommentIsShownAsTheReply()
    {
        var (client, _) = await CreateClientAsync();
        await SubmitAsync(client, "Kan man dela en bostad?");
        var number = _factory.GitHub.Issues.Keys.Single();
        _factory.GitHub.AddComment(number, "Ja — det finns redan under Hantera åtkomst.");

        var reply = (await ListAsync(client)).Single().Reply;
        Assert.NotNull(reply);
        Assert.Equal("Ja — det finns redan under Hantera åtkomst.", reply!.Body);
        Assert.Equal("kallemk", reply.Author);
        Assert.True(reply.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task OpenOrClosedIsReportedSeparatelyFromTheStatusLabel()
    {
        // The confusing case: a status label overrides the derived status, so without a separate
        // IsOpen a closed-but-labelled-"pågår" issue would read as ongoing with no sign it was shut.
        var (client, _) = await CreateClientAsync();
        await SubmitAsync(client, "Något");
        var number = _factory.GitHub.Issues.Keys.Single();
        _factory.GitHub.AddLabel(number, "status:pågår");
        _factory.GitHub.Close(number);

        var item = (await ListAsync(client)).Single();
        Assert.Equal(FeedbackStatus.InProgress, item.Status);
        Assert.False(item.IsOpen);
    }

    [Fact]
    public async Task TheSubmitterMarkerIsNotShownBackToTheUser()
    {
        var (client, _) = await CreateClientAsync();
        await SubmitAsync(client, "Titel", "Min text");

        var item = Assert.Single(await ListAsync(client));
        Assert.DoesNotContain("houseapp:submitter", item.Body);
        Assert.Contains("Min text", item.Body);
    }

    [Fact]
    public async Task SubmittingTooOftenIsRefused()
    {
        // Anyone can sign up now, and this endpoint writes into a private repository.
        var (client, _) = await CreateClientAsync();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, $"Förslag {i}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await SubmitAsync(client, "En till")).StatusCode);
    }

    [Fact]
    public async Task WithoutAuth_ItIsUnauthorized()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/feedback")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitAsync(client, "Smyg")).StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_TheEndpointsSaySoRatherThanFailing()
    {
        _factory.GitHub.IsConfigured = false;
        var (client, _) = await CreateClientAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/feedback")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SubmitAsync(client, "Något")).StatusCode);
    }
}
