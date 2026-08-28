using Jellyfin.Plugin.KeepOrRemove.Inject;
using Xunit;

namespace Jellyfin.Plugin.KeepOrRemove.Tests.Inject;

public class TransformationPatchesTests
{
    [Fact]
    public void Inject_SplicesBothTags_WhenAnchorsPresent()
    {
        const string html = "<html><head></head><body><div></div></body></html>";

        var result = TransformationPatches.Inject(html, "?v=1.2.3");

        Assert.Contains("/KeepOrRemove/kor-vote.css?v=1.2.3", result, StringComparison.Ordinal);
        Assert.Contains("/KeepOrRemove/kor-vote.js?v=1.2.3", result, StringComparison.Ordinal);
        Assert.True(result.IndexOf("kor-vote.css", StringComparison.Ordinal) < result.IndexOf("</head>", StringComparison.Ordinal));
    }

    [Fact]
    public void Inject_ReturnsInputUnchanged_WhenNoAnchors()
    {
        const string html = "not really html";

        Assert.Equal(html, TransformationPatches.Inject(html, "?v=1"));
    }
}
