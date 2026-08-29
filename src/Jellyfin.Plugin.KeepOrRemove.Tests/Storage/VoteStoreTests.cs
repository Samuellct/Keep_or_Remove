using System.Text.Json;
using Jellyfin.Plugin.KeepOrRemove.IO;
using Jellyfin.Plugin.KeepOrRemove.Models;
using Jellyfin.Plugin.KeepOrRemove.Storage;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KeepOrRemove.Tests.Storage;

/// <summary>
/// Covers <see cref="VoteStore"/> persistence: atomic writes, the no-op guard, empty-list
/// persistence, corrupt-file recovery, and write-lock serialization. Uses a real
/// <see cref="FileSystem"/> against a per-test temp directory (mirrors JellyUX-Homepage's
/// UserConfigurationStoreTests), so the tests exercise the actual on-disk behaviour.
/// </summary>
public sealed class VoteStoreTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "kor-votestore-tests-" + Guid.NewGuid());

    private readonly List<VoteStore> _stores = [];

    private string DataDir => Path.Combine(_tempDir, "Jellyfin.Plugin.KeepOrRemove");

    private string VotesFilePath => Path.Combine(DataDir, "votes.json");

    private VoteStore BuildStore(IFileSystem fileSystem, ILogger<VoteStore>? logger = null)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(p => p.DataPath).Returns(_tempDir);

        var store = new VoteStore(
            applicationPaths.Object,
            fileSystem,
            logger ?? NullLogger<VoteStore>.Instance);
        _stores.Add(store);
        return store;
    }

    private static VoteRecord NewVote(Guid? itemId = null, VoteChoice choice = VoteChoice.Keep) => new()
    {
        UserId = Guid.NewGuid(),
        ItemId = itemId ?? Guid.NewGuid(),
        Vote = choice,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Mutate_WhenMutationReturnsTrue_WritesJsonAtomically_NoStrayTempFile()
    {
        var store = BuildStore(new FileSystem());
        var vote = NewVote();

        store.Mutate(votes =>
        {
            votes.Add(vote);
            return true;
        });

        Assert.True(File.Exists(VotesFilePath));
        Assert.False(File.Exists(VotesFilePath + ".tmp"));

        var persisted = JsonSerializer.Deserialize<VotesFile>(File.ReadAllText(VotesFilePath));
        Assert.NotNull(persisted);
        var stored = Assert.Single(persisted!.Votes);
        Assert.Equal(vote.UserId, stored.UserId);
        Assert.Equal(vote.ItemId, stored.ItemId);
        Assert.Equal(VoteChoice.Keep, stored.Vote);

        Assert.Single(store.ReadAll());
    }

    [Fact]
    public void Mutate_WhenMutationReturnsFalse_DoesNotWriteFile()
    {
        var store = BuildStore(new FileSystem());

        store.Mutate(_ => false);

        Assert.False(File.Exists(VotesFilePath));
        Assert.Empty(store.ReadAll());
    }

    [Fact]
    public void Mutate_ClearingToEmptyList_IsPersisted()
    {
        var store = BuildStore(new FileSystem());
        store.Mutate(votes =>
        {
            votes.Add(NewVote());
            return true;
        });

        store.Mutate(votes =>
        {
            votes.Clear();
            return true;
        });

        Assert.True(File.Exists(VotesFilePath));
        Assert.Empty(store.ReadAll());
        Assert.Contains("\"Votes\": []", File.ReadAllText(VotesFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadAll_WhenFileIsCorrupt_ReturnsEmpty_BacksUpOriginal_LogsError()
    {
        var logger = new Mock<ILogger<VoteStore>>();
        var store = BuildStore(new FileSystem(), logger.Object);

        const string garbage = "{ not json";
        File.WriteAllText(VotesFilePath, garbage);

        var result = store.ReadAll();

        Assert.Empty(result);
        Assert.False(File.Exists(VotesFilePath));

        var backups = Directory.GetFiles(DataDir, "votes.json.corrupt-*");
        var backup = Assert.Single(backups);
        Assert.Equal(garbage, File.ReadAllText(backup));

        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Mutate_SecondWriterBlocksUntilFirstReleases_ThenSeesFirstWrite()
    {
        var blocking = new BlockingFileSystem();
        var store = BuildStore(blocking);

        var voteA = NewVote(choice: VoteChoice.Keep);
        var voteB = NewVote(choice: VoteChoice.Remove);

        var writerA = Task.Run(() => store.Mutate(votes =>
        {
            votes.Add(voteA);
            return true;
        }));

        Assert.True(
            blocking.WriteStarted.Wait(TimeSpan.FromSeconds(5)),
            "Writer A never reached the paused Move.");

        List<VoteRecord>? bObserved = null;
        var writerB = Task.Run(() => store.Mutate(votes =>
        {
            bObserved = [.. votes];
            votes.Add(voteB);
            return true;
        }));

        var bFinishedEarly = await Task.WhenAny(writerB, Task.Delay(200)) == writerB;
        Assert.False(bFinishedEarly, "Writer B should block on the write lock while A holds it.");

        blocking.ReleaseWrite.Set();

        await writerA;
        await writerB;

        Assert.NotNull(bObserved);
        Assert.Contains(bObserved!, v => v.ItemId == voteA.ItemId);
        Assert.Equal(2, store.ReadAll().Count);
    }

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp dir under %TEMP% is harmless.
        }
    }

    /// <summary>
    /// Wraps a real <see cref="FileSystem"/> but pauses inside <see cref="Move"/> (the atomic
    /// rename) until <see cref="ReleaseWrite"/> is signalled, so a test can deterministically hold a
    /// writer inside the write lock instead of racing on timing.
    /// </summary>
    private sealed class BlockingFileSystem : IFileSystem
    {
        private readonly FileSystem _inner = new();

        public ManualResetEventSlim WriteStarted { get; } = new(initialState: false);

        public ManualResetEventSlim ReleaseWrite { get; } = new(initialState: false);

        public bool FileExists(string path) => _inner.FileExists(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string contents) => _inner.WriteAllText(path, contents);

        public void Move(string sourceFileName, string destFileName, bool overwrite)
        {
            WriteStarted.Set();
            ReleaseWrite.Wait(TimeSpan.FromSeconds(5));
            _inner.Move(sourceFileName, destFileName, overwrite);
        }

        public void Delete(string path) => _inner.Delete(path);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
    }
}
