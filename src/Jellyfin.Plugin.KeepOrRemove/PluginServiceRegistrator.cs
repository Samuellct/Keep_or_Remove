using Jellyfin.Plugin.KeepOrRemove.Inject;
using Jellyfin.Plugin.KeepOrRemove.IO;
using Jellyfin.Plugin.KeepOrRemove.Services;
using Jellyfin.Plugin.KeepOrRemove.Storage;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.KeepOrRemove;

/// <summary>
/// Registers plugin services into the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IFileSystem, FileSystem>();
        serviceCollection.AddSingleton<IVoteStore, VoteStore>();
        serviceCollection.AddSingleton<IVoteService, VoteService>();
        serviceCollection.AddSingleton<FileTransformationDetector>();
        serviceCollection.AddHostedService<StartupService>();
    }
}
