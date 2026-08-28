namespace Jellyfin.Plugin.KeepOrRemove.Controllers;

/// <summary>
/// Pure authorization decision for endpoints that act on a specific user's vote, to prevent an
/// authenticated user from reading or writing another user's vote by supplying an arbitrary userId
/// (IDOR). Ported from JellyUX-Homepage.
/// </summary>
internal static class UserAccessGuard
{
    /// <summary>
    /// Determines whether the caller is allowed to act on behalf of <paramref name="requestedUserId"/>.
    /// Administrators and server-level API key requests are always allowed; otherwise the requested
    /// user must match the authenticated user.
    /// </summary>
    /// <param name="requestedUserId">The userId the request wants to act on.</param>
    /// <param name="authenticatedUserId">The userId resolved from the request's authorization info.</param>
    /// <param name="isApiKey">Whether the request was authenticated via a server API key.</param>
    /// <param name="isAdministrator">Whether the authenticated user is an administrator.</param>
    /// <returns>True if the request is authorized for the requested user.</returns>
    internal static bool IsAuthorizedForUser(
        Guid requestedUserId,
        Guid authenticatedUserId,
        bool isApiKey,
        bool isAdministrator)
    {
        if (isApiKey || isAdministrator)
        {
            return true;
        }

        return requestedUserId != Guid.Empty && requestedUserId == authenticatedUserId;
    }
}
