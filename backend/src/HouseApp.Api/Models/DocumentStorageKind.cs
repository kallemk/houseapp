using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

/// <summary>
/// Where a document's bytes actually live. Chosen per property, not per document — but stored on the
/// document, because a property that switches backends later must not break the files already
/// uploaded under the old one.
///
/// **Blob must stay 0, and this list is append-only.** There is no HasConversion for
/// Document.StorageKind in AppDbContext, so EF stores it as the underlying integer; every document
/// written before this field existed has no such JSON property at all, which deserializes to 0.
/// Blob being 0 is therefore what makes the whole change migration-free. Reordering or inserting a
/// value silently relabels stored documents.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentStorageKind
{
    Blob,
    Drive,
}
