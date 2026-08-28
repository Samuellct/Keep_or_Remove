using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.KeepOrRemove.Configuration;

/// <summary>
/// Plugin configuration. Deliberately minimal: this plugin is temporary and stores its real data
/// (the votes) in a single JSON file under DataPath, not in this XML. See CLAUDE.md.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the vote buttons are injected into the web client.
    /// When false, the plugin API still works but no buttons appear.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a startup warning surfaced on the configuration page (for example, when the
    /// FileTransformation plugin is missing). Null when there is nothing to report.
    /// </summary>
    public string? StartupWarning { get; set; }
}
