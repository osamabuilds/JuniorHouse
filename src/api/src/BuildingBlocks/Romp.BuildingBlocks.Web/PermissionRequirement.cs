using Microsoft.AspNetCore.Authorization;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// The authorization requirement one endpoint (or group) adds with <c>RequirePermission</c>. A handler decides it per request: the
/// Identity module's handler in S8 (permissions read from the database on every request, SEC-08), a development-only handler before.
/// </summary>
public sealed record PermissionRequirement(PermissionCode Code) : IAuthorizationRequirement;

/// <summary>
/// Endpoint metadata naming a permission, so tooling (the authorization matrix, architecture tests) can read what an endpoint needs
/// without evaluating policies. When a group and a route both declare one, the last (most specific) entry is the route's effective permission.
/// </summary>
public sealed record PermissionMetadata(PermissionCode Code);
