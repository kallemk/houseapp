using System.Text.Json.Serialization;

namespace HouseApp.Api.Dtos.Feedback;

/// <summary>
/// Where a suggestion stands. Derived from the issue's `status:*` label when there is one, otherwise
/// from whether the issue is open — so forgetting to label something is harmless rather than
/// misleading.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeedbackStatus
{
    New,
    Planned,
    InProgress,
    Done,
    Declined,
}

public record FeedbackItemDto(
    int Number,
    string Title,
    string Body,
    FeedbackStatus Status,
    /// <summary>The owner's most recent comment, shown as the explanation. Null when there is none.</summary>
    string? Reply,
    /// <summary>True for the caller's own suggestions, which they see whether published or not.</summary>
    bool IsMine,
    /// <summary>False for suggestions only visible because the caller submitted them or is an admin.</summary>
    bool IsPublished,
    DateTimeOffset CreatedAt);

public record CreateFeedbackRequest(string Title, string Body);
