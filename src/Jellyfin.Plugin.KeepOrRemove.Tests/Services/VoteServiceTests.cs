using Jellyfin.Plugin.KeepOrRemove.Models;
using Jellyfin.Plugin.KeepOrRemove.Services;
using Jellyfin.Plugin.KeepOrRemove.Storage;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KeepOrRemove.Tests.Services;

/// <summary>
/// Covers <see cref="VoteService"/> against every requirement listed in Synthèse.md section 15:
/// vote create / modify-in-place / delete / uniqueness / user isolation, movie and series
/// resolution (episode and season resolve to the parent series, never a distinct row), negative
/// resolution, the admin aggregation with its sorts and type filter, and orphan purge.
/// </summary>
public sealed class VoteServiceTests
{
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();

    private readonly Guid _movieId = Guid.NewGuid();
    private readonly Guid _movie2Id = Guid.NewGuid();
    private readonly Guid _seriesId = Guid.NewGuid();
    private readonly Guid _series2Id = Guid.NewGuid();
    private readonly Guid _episodeId = Guid.NewGuid();
    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _boxSetId = Guid.NewGuid();

    private readonly InMemoryVoteStore _store = new();
    private readonly Mock<ILibraryManager> _library = new();
    private readonly VoteService _service;

    public VoteServiceTests()
    {
        _library.Setup(l => l.GetItemById(_movieId)).Returns(new Movie { Id = _movieId, Name = "Interstellar" });
        _library.Setup(l => l.GetItemById(_movie2Id)).Returns(new Movie { Id = _movie2Id, Name = "The Prestige" });
        _library.Setup(l => l.GetItemById(_seriesId)).Returns(new Series { Id = _seriesId, Name = "Breaking Bad" });
        _library.Setup(l => l.GetItemById(_series2Id)).Returns(new Series { Id = _series2Id, Name = "The Bear" });
        _library.Setup(l => l.GetItemById(_episodeId))
            .Returns(new Episode { Id = _episodeId, SeriesId = _seriesId, SeriesName = "Breaking Bad" });
        _library.Setup(l => l.GetItemById(_seasonId))
            .Returns(new Season { Id = _seasonId, SeriesId = _seriesId });
        _library.Setup(l => l.GetItemById(_boxSetId)).Returns(new BoxSet { Id = _boxSetId, Name = "Trilogy" });
        // Every other id resolves to null (the Moq default), covering the orphan / unresolvable cases.

        _service = new VoteService(_store, _library.Object, NullLogger<VoteService>.Instance);
    }

    // -------------------------------------------------------------------------
    // Votes
    // -------------------------------------------------------------------------

    [Fact]
    public void Upsert_CreatesVote()
    {
        var stored = _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);

        Assert.True(stored);
        var record = Assert.Single(_store.Votes);
        Assert.Equal(_userA, record.UserId);
        Assert.Equal(_movieId, record.ItemId);
        Assert.Equal(VoteChoice.Keep, record.Vote);
        Assert.Equal(VoteChoice.Keep, _service.GetVote(_userA, _movieId));
    }

    [Fact]
    public void Upsert_SameUserAndItem_UpdatesInPlace_NoSecondRow_AdvancesUpdatedAt()
    {
        _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);
        var before = _store.Votes[0].UpdatedAt;

        Thread.Sleep(30);
        _service.UpsertVote(_userA, _movieId, VoteChoice.Remove);

        var record = Assert.Single(_store.Votes);
        Assert.Equal(VoteChoice.Remove, record.Vote);
        Assert.True(record.UpdatedAt > before, "UpdatedAt should advance when a vote is changed.");
    }

    [Fact]
    public void Upsert_TwiceSamePair_YieldsSingleRow()
    {
        _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);
        _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);

        Assert.Single(_store.Votes);
    }

    [Fact]
    public void Delete_RemovesVote()
    {
        _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);

        var removed = _service.DeleteVote(_userA, _movieId);

        Assert.True(removed);
        Assert.Empty(_store.Votes);
        Assert.Null(_service.GetVote(_userA, _movieId));
    }

    [Fact]
    public void Delete_WhenNoVote_ReturnsFalse_WritesNothing()
    {
        var removed = _service.DeleteVote(_userA, _movieId);

        Assert.False(removed);
        Assert.Empty(_store.Votes);
    }

    [Fact]
    public void Isolation_UserBOperationsNeverReadOrOverwriteUserAVote()
    {
        _service.UpsertVote(_userA, _movieId, VoteChoice.Keep);
        _service.UpsertVote(_userB, _movieId, VoteChoice.Remove);

        Assert.Equal(2, _store.Votes.Count);
        Assert.Equal(VoteChoice.Keep, _service.GetVote(_userA, _movieId));
        Assert.Equal(VoteChoice.Remove, _service.GetVote(_userB, _movieId));

        _service.UpsertVote(_userB, _movieId, VoteChoice.Keep);
        _service.DeleteVote(_userB, _movieId);

        Assert.Equal(VoteChoice.Keep, _service.GetVote(_userA, _movieId));
    }

    // -------------------------------------------------------------------------
    // Films
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_Movie_ReturnsSelf()
    {
        Assert.Equal(_movieId, _service.ResolveVoteTargetId(_movieId));
    }

    [Fact]
    public void UpsertThenGet_Movie_Consistent()
    {
        _service.UpsertVote(_userA, _movieId, VoteChoice.Remove);

        Assert.Equal(VoteChoice.Remove, _service.GetVote(_userA, _movieId));
    }

    [Fact]
    public void GetResults_CountsKeepAndRemove_PerMovie()
    {
        Seed(_movieId, keep: 4, remove: 0);
        Seed(_series2Id, keep: 2, remove: 3);

        var results = _service.GetResults(ResultSort.Total, ResultTypeFilter.All);

        var movie = Assert.Single(results, r => r.ItemId == _movieId);
        Assert.Equal(4, movie.Keep);
        Assert.Equal(0, movie.Remove);
        Assert.Equal(4, movie.Total);

        var series = Assert.Single(results, r => r.ItemId == _series2Id);
        Assert.Equal(2, series.Keep);
        Assert.Equal(3, series.Remove);
        Assert.Equal(5, series.Total);
    }

    // -------------------------------------------------------------------------
    // Séries
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_Series_ReturnsSelf()
    {
        Assert.Equal(_seriesId, _service.ResolveVoteTargetId(_seriesId));
    }

    [Fact]
    public void Resolve_Episode_ReturnsParentSeriesId()
    {
        Assert.Equal(_seriesId, _service.ResolveVoteTargetId(_episodeId));
    }

    [Fact]
    public void Resolve_Season_ReturnsParentSeriesId()
    {
        Assert.Equal(_seriesId, _service.ResolveVoteTargetId(_seasonId));
    }

    [Fact]
    public void VoteFromEpisodeThenFromSeasonOfSameSeries_YieldsOneRowOnTheSeries()
    {
        _service.UpsertVote(_userA, _episodeId, VoteChoice.Keep);
        _service.UpsertVote(_userA, _seasonId, VoteChoice.Remove);

        var record = Assert.Single(_store.Votes);
        Assert.Equal(_seriesId, record.ItemId);
        Assert.Equal(VoteChoice.Remove, record.Vote);
    }

    [Fact]
    public void NoStoredRow_EverHasASeasonOrEpisodeItemId()
    {
        _service.UpsertVote(_userA, _episodeId, VoteChoice.Keep);
        _service.UpsertVote(_userB, _seasonId, VoteChoice.Keep);

        Assert.DoesNotContain(_store.Votes, v => v.ItemId == _episodeId || v.ItemId == _seasonId);
        Assert.All(_store.Votes, v => Assert.Equal(_seriesId, v.ItemId));
    }

    // -------------------------------------------------------------------------
    // Résolution négative
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_UnknownId_ReturnsEmpty()
    {
        Assert.Equal(Guid.Empty, _service.ResolveVoteTargetId(Guid.NewGuid()));
    }

    [Fact]
    public void Resolve_EmptyId_ReturnsEmpty()
    {
        Assert.Equal(Guid.Empty, _service.ResolveVoteTargetId(Guid.Empty));
    }

    [Fact]
    public void Resolve_UnsupportedType_BoxSet_ReturnsEmpty()
    {
        Assert.Equal(Guid.Empty, _service.ResolveVoteTargetId(_boxSetId));
    }

    [Fact]
    public void Resolve_EpisodeWithoutSeriesId_ReturnsEmpty()
    {
        var orphanEpisodeId = Guid.NewGuid();
        _library.Setup(l => l.GetItemById(orphanEpisodeId)).Returns(new Episode { Id = orphanEpisodeId });

        Assert.Equal(Guid.Empty, _service.ResolveVoteTargetId(orphanEpisodeId));
    }

    [Fact]
    public void Upsert_UnresolvableItem_ReturnsFalse_WritesNothing()
    {
        var stored = _service.UpsertVote(_userA, _boxSetId, VoteChoice.Keep);

        Assert.False(stored);
        Assert.Empty(_store.Votes);
    }

    [Fact]
    public void GetVote_UnresolvableItem_ReturnsNull()
    {
        Assert.Null(_service.GetVote(_userA, Guid.NewGuid()));
    }

    // -------------------------------------------------------------------------
    // Administration
    // -------------------------------------------------------------------------

    [Fact]
    public void GetResults_SortByKeep_OrdersByKeepDescending()
    {
        Seed(_movieId, keep: 4, remove: 0);   // keep 4
        Seed(_seriesId, keep: 2, remove: 1);  // keep 2
        Seed(_movie2Id, keep: 1, remove: 5);  // keep 1

        var results = _service.GetResults(ResultSort.Keep, ResultTypeFilter.All);

        Assert.Equal(new[] { _movieId, _seriesId, _movie2Id }, results.Select(r => r.ItemId));
    }

    [Fact]
    public void GetResults_SortByRemove_OrdersByRemoveDescending()
    {
        Seed(_movieId, keep: 4, remove: 0);   // remove 0
        Seed(_seriesId, keep: 2, remove: 1);  // remove 1
        Seed(_movie2Id, keep: 1, remove: 5);  // remove 5

        var results = _service.GetResults(ResultSort.Remove, ResultTypeFilter.All);

        Assert.Equal(new[] { _movie2Id, _seriesId, _movieId }, results.Select(r => r.ItemId));
    }

    [Fact]
    public void GetResults_SortByTotal_OrdersByTotalDescending()
    {
        Seed(_movieId, keep: 1, remove: 1);   // total 2
        Seed(_seriesId, keep: 3, remove: 3);  // total 6
        Seed(_movie2Id, keep: 2, remove: 2);  // total 4

        var results = _service.GetResults(ResultSort.Total, ResultTypeFilter.All);

        Assert.Equal(new[] { _seriesId, _movie2Id, _movieId }, results.Select(r => r.ItemId));
    }

    [Fact]
    public void GetResults_FilterMovies_ReturnsOnlyMovies()
    {
        Seed(_movieId, keep: 1, remove: 0);
        Seed(_seriesId, keep: 1, remove: 0);

        var results = _service.GetResults(ResultSort.Total, ResultTypeFilter.Movies);

        var row = Assert.Single(results);
        Assert.Equal(_movieId, row.ItemId);
        Assert.Equal("Movie", row.Type);
    }

    [Fact]
    public void GetResults_FilterSeries_ReturnsOnlySeries()
    {
        Seed(_movieId, keep: 1, remove: 0);
        Seed(_seriesId, keep: 1, remove: 0);

        var results = _service.GetResults(ResultSort.Total, ResultTypeFilter.Series);

        var row = Assert.Single(results);
        Assert.Equal(_seriesId, row.ItemId);
        Assert.Equal("Series", row.Type);
    }

    [Fact]
    public void GetResults_ItemWithNoVotes_IsAbsent()
    {
        Seed(_movieId, keep: 1, remove: 0);
        // _movie2Id resolves in the library but has no votes.

        var results = _service.GetResults(ResultSort.Total, ResultTypeFilter.All);

        Assert.DoesNotContain(results, r => r.ItemId == _movie2Id);
        Assert.Single(results);
    }

    // -------------------------------------------------------------------------
    // Purge
    // -------------------------------------------------------------------------

    [Fact]
    public void PurgeOrphans_RemovesVotesWithNoResolvableItem_KeepsOthers_ReturnsCount()
    {
        var orphanId = Guid.NewGuid();
        Seed(_movieId, keep: 2, remove: 1);
        Seed(orphanId, keep: 1, remove: 1);

        var removed = _service.PurgeOrphans();

        Assert.Equal(2, removed);
        Assert.All(_store.Votes, v => Assert.Equal(_movieId, v.ItemId));
        Assert.Equal(3, _store.Votes.Count);
    }

    [Fact]
    public void PurgeOrphans_WhenNoOrphans_ReturnsZero_WritesNothing()
    {
        Seed(_movieId, keep: 1, remove: 0);

        Assert.Equal(0, _service.PurgeOrphans());
        Assert.Single(_store.Votes);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void Seed(Guid itemId, int keep, int remove)
    {
        for (var i = 0; i < keep; i++)
        {
            _store.Votes.Add(new VoteRecord
            {
                UserId = Guid.NewGuid(),
                ItemId = itemId,
                Vote = VoteChoice.Keep,
                UpdatedAt = DateTime.UtcNow
            });
        }

        for (var i = 0; i < remove; i++)
        {
            _store.Votes.Add(new VoteRecord
            {
                UserId = Guid.NewGuid(),
                ItemId = itemId,
                Vote = VoteChoice.Remove,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private sealed class InMemoryVoteStore : IVoteStore
    {
        public List<VoteRecord> Votes { get; } = [];

        public IReadOnlyList<VoteRecord> ReadAll() => [.. Votes];

        public void Mutate(Func<List<VoteRecord>, bool> mutation) => mutation(Votes);
    }
}
