using System.Reflection;
using Jellyfin.Plugin.KeepOrRemove.Models;
using Jellyfin.Plugin.KeepOrRemove.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KeepOrRemove.Controllers;

/// <summary>
/// HTTP API for Keep or Remove. Route: <c>/KeepOrRemove</c>.
/// </summary>
[ApiController]
[Route("KeepOrRemove")]
public class VoteController : ControllerBase
{
    private static readonly Assembly PluginAssembly = typeof(VoteController).Assembly;

    private readonly IVoteService _voteService;
    private readonly IAuthorizationContext _authContext;
    private readonly ILogger<VoteController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoteController"/> class.
    /// </summary>
    /// <param name="voteService">The vote business-logic service.</param>
    /// <param name="authContext">Jellyfin request authorization context.</param>
    /// <param name="logger">Logger.</param>
    public VoteController(IVoteService voteService, IAuthorizationContext authContext, ILogger<VoteController> logger)
    {
        _voteService = voteService;
        _authContext = authContext;
        _logger = logger;
    }

    /// <summary>Gets the current user's vote for an item.</summary>
    /// <param name="itemId">The media item id (resolved to its parent series for episodes/seasons).</param>
    /// <returns>An object with a <c>vote</c> of "KEEP", "REMOVE", or null.</returns>
    [HttpGet("vote")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetVote([FromQuery] Guid itemId)
    {
        var userId = await CurrentUserIdAsync().ConfigureAwait(false);
        return Wrap(() =>
        {
            var vote = _voteService.GetVote(userId, itemId);
            return Ok(new { vote = vote?.ToString().ToUpperInvariant() });
        });
    }

    /// <summary>Creates or updates the current user's vote for an item.</summary>
    /// <param name="request">The vote request.</param>
    /// <returns>204 on success, 404 when the item cannot be resolved.</returns>
    [HttpPut("vote")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> PutVote([FromBody] VoteRequest request)
    {
        if (request is null || !Enum.TryParse<VoteChoice>(request.Vote, ignoreCase: true, out var choice))
        {
            return BadRequest();
        }

        var userId = await CurrentUserIdAsync().ConfigureAwait(false);
        return Wrap(() => _voteService.UpsertVote(userId, request.ItemId, choice) ? NoContent() : NotFound());
    }

    /// <summary>Removes the current user's vote for an item.</summary>
    /// <param name="itemId">The media item id.</param>
    /// <returns>204 always (idempotent).</returns>
    [HttpDelete("vote")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteVote([FromQuery] Guid itemId)
    {
        var userId = await CurrentUserIdAsync().ConfigureAwait(false);
        return Wrap(() =>
        {
            _voteService.DeleteVote(userId, itemId);
            return NoContent();
        });
    }

    /// <summary>Gets the aggregated results table. Administrators only.</summary>
    /// <param name="sort">"keep", "remove", or "total" (default).</param>
    /// <param name="type">"movie", "series", or "all" (default).</param>
    /// <returns>The aggregated rows.</returns>
    [HttpGet("admin/results")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetResults([FromQuery] string? sort, [FromQuery] string? type)
    {
        var sortValue = Enum.TryParse<ResultSort>(sort, ignoreCase: true, out var s) ? s : ResultSort.Total;
        var typeValue = type?.ToLowerInvariant() switch
        {
            "movie" or "movies" => ResultTypeFilter.Movies,
            "series" => ResultTypeFilter.Series,
            _ => ResultTypeFilter.All
        };

        return Wrap(() =>
        {
            var rows = _voteService.GetResults(sortValue, typeValue)
                .Select(a => new { itemId = a.ItemId, name = a.Name, type = a.Type, keep = a.Keep, remove = a.Remove, total = a.Total });
            return Ok(rows);
        });
    }

    /// <summary>Removes every vote whose media no longer exists. Administrators only.</summary>
    /// <returns>An object with the number of votes removed.</returns>
    [HttpPost("admin/purge")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult PurgeOrphans() => Wrap(() => Ok(new { removed = _voteService.PurgeOrphans() }));

    /// <summary>Serves the embedded vote-button script.</summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("kor-vote.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public IActionResult GetScript() => Asset("kor-vote.js", "application/javascript");

    /// <summary>Serves the embedded vote-button stylesheet.</summary>
    /// <returns>The CSS file.</returns>
    [HttpGet("kor-vote.css")]
    [AllowAnonymous]
    [Produces("text/css")]
    public IActionResult GetStylesheet() => Asset("kor-vote.css", "text/css");

    /// <summary>Serves the embedded admin config-page script.</summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("config.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public IActionResult GetConfigScript() => Asset("config.js", "application/javascript");

    private async Task<Guid> CurrentUserIdAsync()
    {
        var authInfo = await _authContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        return authInfo.UserId;
    }

    private ActionResult Wrap(Func<ActionResult> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger.LogError(ex, "[KeepOrRemove] Vote storage is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private IActionResult Asset(string suffix, string contentType)
    {
        var name = PluginAssembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(PluginAssembly.GetManifestResourceStream(name)!, contentType);
    }
}

/// <summary>Request body for <c>PUT /KeepOrRemove/vote</c>.</summary>
public sealed class VoteRequest
{
    /// <summary>Gets or sets the media item id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the vote: "KEEP" or "REMOVE".</summary>
    public string Vote { get; set; } = string.Empty;
}
