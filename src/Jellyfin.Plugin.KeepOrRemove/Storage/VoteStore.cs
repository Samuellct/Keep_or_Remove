using System.Text.Json;
using Jellyfin.Plugin.KeepOrRemove.IO;
using Jellyfin.Plugin.KeepOrRemove.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KeepOrRemove.Storage;

/// <summary>
/// The single source of persistence for the plugin: reads and writes
/// <c>{DataPath}/Jellyfin.Plugin.KeepOrRemove/votes.json</c>.
/// <para>
/// Thread-safe via a <see cref="ReaderWriterLockSlim"/>; writes are atomic (temp file then rename),
/// mirroring JellyUX-Homepage's disk-persistence pattern. Kept deliberately under <c>DataPath</c>
/// (not <c>PluginConfigurationsPath</c>) so the whole plugin footprint is one directory that can be
/// deleted on uninstall with no trace.
/// </para>
/// </summary>
public sealed class VoteStore : IVoteStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<VoteStore> _logger;
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoteStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Provides the application data directory path.</param>
    /// <param name="fileSystem">File system abstraction, for testability.</param>
    /// <param name="logger">Logger.</param>
    public VoteStore(IApplicationPaths applicationPaths, IFileSystem fileSystem, ILogger<VoteStore> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _directory = Path.Combine(applicationPaths.DataPath, "Jellyfin.Plugin.KeepOrRemove");
        _filePath = Path.Combine(_directory, "votes.json");
        _fileSystem.CreateDirectory(_directory);
    }

    /// <inheritdoc/>
    public IReadOnlyList<VoteRecord> ReadAll()
    {
        _lock.EnterReadLock();
        try
        {
            return ReadUnlocked().Votes;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void Mutate(Func<List<VoteRecord>, bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        _lock.EnterWriteLock();
        try
        {
            var file = ReadUnlocked();
            if (mutation(file.Votes))
            {
                WriteUnlocked(file);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private VotesFile ReadUnlocked()
    {
        if (!_fileSystem.FileExists(_filePath))
        {
            return new VotesFile();
        }

        try
        {
            var json = _fileSystem.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<VotesFile>(json) ?? new VotesFile();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Never let a corrupt file crash the server. Back it up next to the original so it can
            // be inspected, then start fresh.
            _logger.LogError(ex, "[KeepOrRemove] votes.json is unreadable; backing it up and starting fresh.");
            TryBackupCorruptFile();
            return new VotesFile();
        }
    }

    private void WriteUnlocked(VotesFile file)
    {
        var tmp = _filePath + ".tmp";
        _fileSystem.WriteAllText(tmp, JsonSerializer.Serialize(file, SerializerOptions));
        _fileSystem.Move(tmp, _filePath, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backup = $"{_filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _fileSystem.Move(_filePath, backup, overwrite: false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "[KeepOrRemove] Could not back up the corrupt votes.json.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;
    }
}
