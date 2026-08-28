using Jellyfin.Plugin.KeepOrRemove.Models;
using Jellyfin.Plugin.KeepOrRemove.Storage;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KeepOrRemove.Services;

/// <summary>
/// Business logic for votes: target resolution (episode/season -> parent series), upsert with
/// (UserId, ItemId) uniqueness, per-user reads, aggregation, and orphan cleanup.
/// The plugin never acts on the library - this class only reads item metadata to resolve and label.
/// </summary>
public sealed class VoteService : IVoteService
{
    private readonly IVoteStore _store;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<VoteService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoteService"/> class.
    /// </summary>
    /// <param name="store">The vote persistence store.</param>
    /// <param name="libraryManager">Jellyfin library manager, used only to resolve and label items.</param>
    /// <param name="logger">Logger.</param>
    public VoteService(IVoteStore store, ILibraryManager libraryManager, ILogger<VoteService> logger)
    {
        _store = store;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Guid ResolveVoteTargetId(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            return Guid.Empty;
        }

        var item = _libraryManager.GetItemById(itemId);
        return item switch
        {
            null => Guid.Empty,
            Movie => itemId,
            Series => itemId,
            Episode episode when episode.SeriesId != Guid.Empty => episode.SeriesId,
            Season season when season.SeriesId != Guid.Empty => season.SeriesId,
            _ => Guid.Empty
        };
    }

    /// <inheritdoc/>
    public VoteChoice? GetVote(Guid userId, Guid itemId)
    {
        var targetId = ResolveVoteTargetId(itemId);
        if (targetId == Guid.Empty)
        {
            return null;
        }

        var match = _store.ReadAll()
            .FirstOrDefault(v => v.UserId == userId && v.ItemId == targetId);
        return match?.Vote;
    }

    /// <inheritdoc/>
    public bool UpsertVote(Guid userId, Guid itemId, VoteChoice choice)
    {
        var targetId = ResolveVoteTargetId(itemId);
        if (targetId == Guid.Empty)
        {
            _logger.LogWarning("[KeepOrRemove] Ignoring vote for unresolvable item {ItemId}.", itemId);
            return false;
        }

        _store.Mutate(votes =>
        {
            var existing = votes.FirstOrDefault(v => v.UserId == userId && v.ItemId == targetId);
            if (existing is not null)
            {
                existing.Vote = choice;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                votes.Add(new VoteRecord
                {
                    UserId = userId,
                    ItemId = targetId,
                    Vote = choice,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return true;
        });

        return true;
    }

    /// <inheritdoc/>
    public bool DeleteVote(Guid userId, Guid itemId)
    {
        var targetId = ResolveVoteTargetId(itemId);
        if (targetId == Guid.Empty)
        {
            return false;
        }

        var removed = false;
        _store.Mutate(votes => removed = votes.RemoveAll(v => v.UserId == userId && v.ItemId == targetId) > 0);
        return removed;
    }

    /// <inheritdoc/>
    public IReadOnlyList<VoteAggregate> GetResults(ResultSort sort, ResultTypeFilter typeFilter)
    {
        var aggregates = _store.ReadAll()
            .GroupBy(v => v.ItemId)
            .Select(BuildAggregate)
            .Where(a => Matches(a, typeFilter))
            .ToList();

        IEnumerable<VoteAggregate> ordered = sort switch
        {
            ResultSort.Keep => aggregates.OrderByDescending(a => a.Keep).ThenByDescending(a => a.Total),
            ResultSort.Remove => aggregates.OrderByDescending(a => a.Remove).ThenByDescending(a => a.Total),
            _ => aggregates.OrderByDescending(a => a.Total).ThenByDescending(a => a.Keep)
        };

        return ordered.ToList();
    }

    /// <inheritdoc/>
    public int PurgeOrphans()
    {
        var removed = 0;
        _store.Mutate(votes =>
        {
            var before = votes.Count;
            votes.RemoveAll(v => _libraryManager.GetItemById(v.ItemId) is null);
            removed = before - votes.Count;
            return removed > 0;
        });

        if (removed > 0)
        {
            _logger.LogInformation("[KeepOrRemove] Purged {Count} orphan vote(s).", removed);
        }

        return removed;
    }

    private VoteAggregate BuildAggregate(IGrouping<Guid, VoteRecord> group)
    {
        var item = _libraryManager.GetItemById(group.Key);
        var name = item?.Name ?? group.Key.ToString();
        var type = item switch
        {
            Movie => "Movie",
            Series => "Series",
            null => "Unknown",
            _ => item.GetType().Name
        };

        var keep = group.Count(v => v.Vote == VoteChoice.Keep);
        var remove = group.Count(v => v.Vote == VoteChoice.Remove);
        return new VoteAggregate(group.Key, name, type, keep, remove);
    }

    private static bool Matches(VoteAggregate aggregate, ResultTypeFilter filter) => filter switch
    {
        ResultTypeFilter.Movies => aggregate.Type == "Movie",
        ResultTypeFilter.Series => aggregate.Type == "Series",
        _ => true
    };
}
