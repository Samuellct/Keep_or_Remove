using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.KeepOrRemove.Models;

/// <summary>
/// The binary preference a user can express for a media item.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VoteChoice
{
    /// <summary>The user wants the media kept in the library.</summary>
    Keep,

    /// <summary>The user is fine with the media being removed.</summary>
    Remove
}

/// <summary>
/// A single stored vote. The pair (<see cref="UserId"/>, <see cref="ItemId"/>) is unique:
/// changing a vote updates this record in place, it never creates a second one.
/// <see cref="ItemId"/> is always a Movie or a Series id (episode/season contexts are resolved to
/// the parent series before storing).
/// </summary>
public sealed class VoteRecord
{
    /// <summary>Gets or sets the Jellyfin user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the Jellyfin item id (Movie or Series).</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the vote choice.</summary>
    public VoteChoice Vote { get; set; }

    /// <summary>Gets or sets the UTC timestamp the vote was last set or changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// On-disk shape of <c>votes.json</c>.
/// </summary>
public sealed class VotesFile
{
    /// <summary>
    /// Gets or sets the storage schema version. A plain integer for a possible future one-shot
    /// in-place correction, not a migrations framework. Deleting the file is always a valid reset.
    /// </summary>
    public int Schema { get; set; } = 1;

    /// <summary>Gets or sets the stored votes.</summary>
    public List<VoteRecord> Votes { get; set; } = [];
}

/// <summary>
/// One row of the admin results table: aggregated counts for a single media item.
/// </summary>
/// <param name="ItemId">The media item id.</param>
/// <param name="Name">The media display name, or the raw id if it can no longer be resolved.</param>
/// <param name="Type">"Movie", "Series", or "Unknown" (orphan).</param>
/// <param name="Keep">Number of KEEP votes.</param>
/// <param name="Remove">Number of REMOVE votes.</param>
public readonly record struct VoteAggregate(Guid ItemId, string Name, string Type, int Keep, int Remove)
{
    /// <summary>Gets the total number of votes for this item.</summary>
    public int Total => Keep + Remove;
}
