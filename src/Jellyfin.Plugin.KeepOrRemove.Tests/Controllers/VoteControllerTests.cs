using System.Reflection;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.KeepOrRemove.Controllers;
using Jellyfin.Plugin.KeepOrRemove.Models;
using Jellyfin.Plugin.KeepOrRemove.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KeepOrRemove.Tests.Controllers;

/// <summary>
/// Covers <see cref="VoteController"/>: the authorization attributes are present and correct
/// (reflection), no action exposes a client-supplied userId (no IDOR surface), the caller identity
/// is taken from <see cref="IAuthorizationContext"/>, the vote payload is validated, and
/// <see cref="VoteController"/>'s private <c>Wrap</c> maps storage failures to 503.
///
/// Actual policy enforcement is Jellyfin middleware, not this plugin's code (R5 clause in TODO_V1.md
/// 2.3); a non-admin getting 403 on the admin routes is verified by manual test in Phase 5/6.
/// </summary>
public sealed class VoteControllerTests
{
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Authorization attributes (reflection)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(VoteController.GetResults))]
    [InlineData(nameof(VoteController.PurgeOrphans))]
    public void AdminEndpoints_RequireElevation(string methodName)
    {
        var authorize = Method(methodName).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(Policies.RequiresElevation, authorize!.Policy);
    }

    [Theory]
    [InlineData(nameof(VoteController.GetVote))]
    [InlineData(nameof(VoteController.PutVote))]
    [InlineData(nameof(VoteController.DeleteVote))]
    public void VoteEndpoints_RequireAuthentication_WithoutElevation_NotAnonymous(string methodName)
    {
        var method = Method(methodName);
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.True(string.IsNullOrEmpty(authorize!.Policy), "Vote endpoints must not require elevation.");
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(nameof(VoteController.GetScript))]
    [InlineData(nameof(VoteController.GetStylesheet))]
    [InlineData(nameof(VoteController.GetConfigScript))]
    public void AssetEndpoints_AreAnonymous(string methodName)
    {
        Assert.NotNull(Method(methodName).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void NoActionMethodExposesAUserIdParameter()
    {
        var actions = typeof(VoteController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes(inherit: false).OfType<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            Assert.DoesNotContain(
                action.GetParameters(),
                p => p.Name is not null && p.Name.Contains("user", StringComparison.OrdinalIgnoreCase));
        }
    }

    // -------------------------------------------------------------------------
    // Caller identity comes from IAuthorizationContext
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetVote_PassesTheAuthenticatedUserIdToTheService()
    {
        var knownUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new Mock<IVoteService>();
        var controller = BuildController(service, AuthReturning(knownUserId));

        await controller.GetVote(itemId);

        service.Verify(s => s.GetVote(knownUserId, itemId), Times.Once);
    }

    [Fact]
    public async Task PutVote_PassesTheAuthenticatedUserIdToTheService()
    {
        var knownUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new Mock<IVoteService>();
        service.Setup(s => s.UpsertVote(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<VoteChoice>())).Returns(true);
        var controller = BuildController(service, AuthReturning(knownUserId));

        await controller.PutVote(new VoteRequest { ItemId = itemId, Vote = "KEEP" });

        service.Verify(s => s.UpsertVote(knownUserId, itemId, VoteChoice.Keep), Times.Once);
    }

    [Fact]
    public async Task DeleteVote_PassesTheAuthenticatedUserIdToTheService()
    {
        var knownUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new Mock<IVoteService>();
        var controller = BuildController(service, AuthReturning(knownUserId));

        await controller.DeleteVote(itemId);

        service.Verify(s => s.DeleteVote(knownUserId, itemId), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Payload validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("KEEP")]
    [InlineData("keep")]
    [InlineData("REMOVE")]
    [InlineData("Remove")]
    public async Task PutVote_WithAValidChoice_DoesNotReturnBadRequest(string vote)
    {
        var service = new Mock<IVoteService>();
        service.Setup(s => s.UpsertVote(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<VoteChoice>())).Returns(true);
        var controller = BuildController(service);

        var result = await controller.PutVote(new VoteRequest { ItemId = Guid.NewGuid(), Vote = vote });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutVote_WithNullBody_ReturnsBadRequest()
    {
        var controller = BuildController();

        var result = await controller.PutVote(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Theory]
    [InlineData("MAYBE")]   // unknown word
    [InlineData("99")]      // numeric, out of the enum range - Enum.TryParse would accept this
    [InlineData("1")]       // numeric, in range - Enum.TryParse would accept this as Remove
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PutVote_WithAnythingOtherThanTheTwoAllowedValues_ReturnsBadRequest(string vote)
    {
        var controller = BuildController();

        var result = await controller.PutVote(new VoteRequest { ItemId = Guid.NewGuid(), Vote = vote });

        Assert.IsType<BadRequestResult>(result);
    }

    // -------------------------------------------------------------------------
    // GetResults query mapping
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("keep", ResultSort.Keep)]
    [InlineData("remove", ResultSort.Remove)]
    [InlineData("total", ResultSort.Total)]
    [InlineData(null, ResultSort.Total)]
    [InlineData("garbage", ResultSort.Total)]
    public void GetResults_MapsTheSortQueryParameter(string? sort, ResultSort expected)
    {
        var service = new Mock<IVoteService>();
        service.Setup(s => s.GetResults(It.IsAny<ResultSort>(), It.IsAny<ResultTypeFilter>()))
            .Returns([]);
        var controller = BuildController(service);

        controller.GetResults(sort, type: null);

        service.Verify(s => s.GetResults(expected, It.IsAny<ResultTypeFilter>()), Times.Once);
    }

    [Theory]
    [InlineData("movie", ResultTypeFilter.Movies)]
    [InlineData("movies", ResultTypeFilter.Movies)]
    [InlineData("series", ResultTypeFilter.Series)]
    [InlineData(null, ResultTypeFilter.All)]
    [InlineData("garbage", ResultTypeFilter.All)]
    public void GetResults_MapsTheTypeQueryParameter(string? type, ResultTypeFilter expected)
    {
        var service = new Mock<IVoteService>();
        service.Setup(s => s.GetResults(It.IsAny<ResultSort>(), It.IsAny<ResultTypeFilter>()))
            .Returns([]);
        var controller = BuildController(service);

        controller.GetResults(sort: null, type);

        service.Verify(s => s.GetResults(It.IsAny<ResultSort>(), expected), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Wrap: storage failures map to 503 (Synthèse section 16)
    // -------------------------------------------------------------------------

    [Fact]
    public void Wrap_WhenActionThrowsIOException_Returns503()
    {
        var result = InvokeWrap(BuildController(), () => throw new IOException("disk gone"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void Wrap_WhenActionThrowsInvalidOperationException_Returns503()
    {
        var result = InvokeWrap(BuildController(), () => throw new InvalidOperationException());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void Wrap_WhenActionThrowsUnauthorizedAccessException_Returns503()
    {
        // An unreadable votes.json throws UnauthorizedAccessException (not an IOException); it must
        // still map to a clean 503, per Synthèse section 16.
        var result = InvokeWrap(BuildController(), () => throw new UnauthorizedAccessException());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void Wrap_WhenActionSucceeds_ReturnsItsResult()
    {
        var expected = new OkResult();

        var result = InvokeWrap(BuildController(), () => expected);

        Assert.Same(expected, result);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MethodInfo Method(string name) => typeof(VoteController).GetMethod(name)!;

    private static VoteController BuildController(
        Mock<IVoteService>? service = null,
        Mock<IAuthorizationContext>? auth = null) =>
        new(
            (service ?? new Mock<IVoteService>()).Object,
            (auth ?? AuthReturning(DefaultUserId)).Object,
            NullLogger<VoteController>.Instance);

    private static Mock<IAuthorizationContext> AuthReturning(Guid userId)
    {
        var mock = new Mock<IAuthorizationContext>();
        var user = new User("test", "test", "test") { Id = userId };
        mock.Setup(a => a.GetAuthorizationInfo(It.IsAny<HttpContext>()))
            .ReturnsAsync(new AuthorizationInfo { User = user });
        return mock;
    }

    private static ActionResult InvokeWrap(VoteController controller, Func<ActionResult> action)
    {
        var wrap = typeof(VoteController).GetMethod("Wrap", BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            return (ActionResult)wrap.Invoke(controller, [action])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
