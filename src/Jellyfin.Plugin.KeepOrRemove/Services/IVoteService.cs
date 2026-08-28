using Jellyfin.Plugin.KeepOrRemove.Models;

namespace Jellyfin.Plugin.KeepOrRemove.Services;

/// <summary>Sort order for the admin results table.</summary>
public enum ResultSort
{
    /// <summary>By total votes, descending.</summary>
    Total,

    /// <summary>By KEEP votes, descending.</summary>
    Keep,

    /// <summary>By REMOVE votes, descending.</summary>
    Remove
}

/// <summary>Media-type filter for the admin results table.</summary>
public enum ResultTypeFilter
{
    /// <summary>All media types.</summary>
    All,

    /// <summary>Movies only.</summary>
    Movies,

    /// <summary>Series only.</summary>
    Series
}

/// <summary>
/// Business logic for votes. See <see cref="VoteService"/>.
/// </summary>
public interface IVoteService
{
    /// <summary>
    /// Resolves a media item id to the id the vote is actually stored against: the item itself for a
    /// Movie or Series, the parent Series for an Episode or Season, or <see cref="Guid.Empty"/> when
    /// the item cannot be resolved or is not a supported target.
    /// </summary>
    /// <param name="itemId">The item id from the client.</param>
    /// <returns>The resolved target id, or <see cref="Guid.Empty"/>.</returns>
    Guid ResolveVoteTargetId(Guid itemId);

    /// <summary>Gets a user's current vote for an item, or null if they have not voted.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="itemId">The item id from the client (resolved internally).</param>
    /// <returns>The vote choice, or null.</returns>
    VoteChoice? GetVote(Guid userId, Guid itemId);

    /// <summary>Creates or updates a user's vote for an item.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="itemId">The item id from the client (resolved internally).</param>
    /// <param name="choice">The vote choice.</param>
    /// <returns>True if the vote was stored; false if the item could not be resolved.</returns>
    bool UpsertVote(Guid userId, Guid itemId, VoteChoice choice);

    /// <summary>Removes a user's vote for an item, if present.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="itemId">The item id from the client (resolved internally).</param>
    /// <returns>True if a vote was removed.</returns>
    bool DeleteVote(Guid userId, Guid itemId);

    /// <summary>Returns the aggregated results for every item with at least one vote.</summary>
    /// <param name="sort">The sort order.</param>
    /// <param name="typeFilter">The media-type filter.</param>
    /// <returns>The aggregated rows.</returns>
    IReadOnlyList<VoteAggregate> GetResults(ResultSort sort, ResultTypeFilter typeFilter);

    /// <summary>Removes every vote whose item no longer exists in the library.</summary>
    /// <returns>The number of votes removed.</returns>
    int PurgeOrphans();
}
