using Jellyfin.Plugin.KeepOrRemove.Models;

namespace Jellyfin.Plugin.KeepOrRemove.Inject;

/// <summary>
/// Static transformation callbacks invoked by the FileTransformation plugin via reflection.
/// Unlike JellyUX-Homepage, this plugin patches nothing but <c>index.html</c> - no minified webpack
/// chunks - so there is no bundle-drift risk surface here.
/// </summary>
public static class TransformationPatches
{
    /// <summary>
    /// Injects the Keep or Remove CSS link and JS script tags into Jellyfin's index.html.
    /// Called by FileTransformation via reflection - must remain public and static.
    /// </summary>
    /// <param name="content">Payload containing the raw index.html contents.</param>
    /// <returns>Transformed HTML with the plugin resources injected, or the input unchanged if the
    /// expected anchors are absent.</returns>
    public static string IndexHtml(PatchRequestPayload content)
    {
        var raw = content.Contents ?? string.Empty;
        return Inject(raw, VersionQuery());
    }

    /// <summary>
    /// Pure splice logic, decoupled from reflection for testing.
    /// </summary>
    /// <param name="raw">The raw index.html contents.</param>
    /// <param name="cacheParam">The <c>?v=...</c> cache-busting query string.</param>
    /// <returns>The transformed content, or the input unchanged when an anchor is missing.</returns>
    internal static string Inject(string raw, string cacheParam)
    {
        var linkTag = $"<link rel=\"stylesheet\" href=\"/KeepOrRemove/kor-vote.css{cacheParam}\" />";
        var scriptTag = $"<script src=\"/KeepOrRemove/kor-vote.js{cacheParam}\" defer></script>";

        var result = raw;
        if (result.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("</head>", $"{linkTag}\n</head>", StringComparison.OrdinalIgnoreCase);
        }

        if (result.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("</body>", $"{scriptTag}\n</body>", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string VersionQuery()
    {
        var version = Plugin.Instance?.Version?.ToString() ?? "0";
        return $"?v={version}";
    }
}
