using Jellyfin.Plugin.KeepOrRemove.Models;

namespace Jellyfin.Plugin.KeepOrRemove.Storage;

/// <summary>
/// Persistence contract for stored votes. See <see cref="VoteStore"/>.
/// </summary>
public interface IVoteStore
{
    /// <summary>Returns a snapshot of every stored vote.</summary>
    /// <returns>All stored votes.</returns>
    IReadOnlyList<VoteRecord> ReadAll();

    /// <summary>
    /// Applies a mutation to the vote list under a write lock. The mutation receives the live list
    /// and returns true if it changed anything (only then is the file rewritten).
    /// </summary>
    /// <param name="mutation">The mutation to apply; returns true when it modified the list.</param>
    void Mutate(Func<List<VoteRecord>, bool> mutation);
}
