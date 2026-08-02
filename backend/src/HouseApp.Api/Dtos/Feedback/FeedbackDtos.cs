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

/// <summary>The owner's latest reply — who wrote it and when, not just what it said.</summary>
public record FeedbackReplyDto(string Body, string Author, DateTimeOffset CreatedAt);

public record FeedbackItemDto(
    int Number,
    string Title,
    string Body,
    FeedbackStatus Status,
    /// <summary>
    /// Whether the underlying issue is still open. Reported **separately from Status** on purpose: a
    /// `status:*` label overrides the derived status, so without this an issue labelled "pågår" but
    /// closed would show as ongoing with no hint that it had been closed.
    /// </summary>
    bool IsOpen,
    /// <summary>The owner's most recent comment, shown as the explanation. Null when there is none.</summary>
    FeedbackReplyDto? Reply,
    /// <summary>True for the caller's own suggestions, which they see whether published or not.</summary>
    bool IsMine,
    /// <summary>False for suggestions only visible because the caller submitted them or is an admin.</summary>
    bool IsPublished,
    DateTimeOffset CreatedAt);

public record CreateFeedbackRequest(string Title, string Body);
