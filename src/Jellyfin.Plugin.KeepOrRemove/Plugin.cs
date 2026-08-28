using Jellyfin.Plugin.KeepOrRemove.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.KeepOrRemove;

/// <summary>
/// Keep or Remove plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasPluginConfiguration, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the singleton plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc/>
    public override string Name => "Keep or Remove";

    /// <inheritdoc/>
    public override Guid Id => Guid.Parse("dbcf4f1f-bc0c-4681-b79a-cbd2294b2538");

    /// <inheritdoc/>
    public override string Description =>
        "Users vote keep or remove on library media to help the admin decide manual library rotation. Never modifies the library.";

    /// <inheritdoc/>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Web.config.html"
        };
    }
}
