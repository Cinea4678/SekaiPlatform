namespace SekaiPlatform.Shared.Web.Auth;

/// <summary>
/// Defines internal token claim names, service actors, and scopes.
/// </summary>
public static class SekaiInternalAuthDefaults
{
    /// <summary>
    /// Claim type carrying the calling service identity.
    /// </summary>
    public const string ActorClaimType = "actor";

    /// <summary>
    /// Claim type carrying the internal capability granted to the call.
    /// </summary>
    public const string ScopeClaimType = "scope";

    /// <summary>
    /// Claim type carrying the delegated platform user identifier.
    /// </summary>
    public const string SubjectUserIdClaimType = "subject_user_id";

    /// <summary>
    /// API Service actor name used in internal tokens.
    /// </summary>
    public const string ApiServiceActor = "api-service";

    /// <summary>
    /// Auth Service audience and actor name.
    /// </summary>
    public const string AuthServiceActor = "auth-service";

    /// <summary>
    /// Asset Service audience and actor name.
    /// </summary>
    public const string AssetServiceActor = "asset-service";

    /// <summary>
    /// Search Service audience and actor name.
    /// </summary>
    public const string SearchServiceActor = "search-service";

    /// <summary>
    /// Sync Worker actor name used in internal tokens.
    /// </summary>
    public const string SyncWorkerActor = "sync-worker";

    /// <summary>
    /// Scope for proxying user login to Auth Service.
    /// </summary>
    public const string AuthLoginScope = "auth.login";

    /// <summary>
    /// Scope for reading a user's current session from Auth Service.
    /// </summary>
    public const string AuthSessionReadScope = "auth.session.read";

    /// <summary>
    /// Scope for reading a user's tenant memberships from Auth Service.
    /// </summary>
    public const string AuthTenantsReadScope = "auth.tenants.read";

    /// <summary>
    /// Scope for switching a user's current tenant through Auth Service.
    /// </summary>
    public const string AuthTenantSwitchScope = "auth.tenant.switch";

    /// <summary>
    /// Scope for inviting users through Auth Service.
    /// </summary>
    public const string UsersInvitationsWriteScope = "users.invitations.write";

    /// <summary>
    /// Scope for creating source synchronization jobs through Asset Service.
    /// </summary>
    public const string SyncJobsWriteScope = "sync.jobs.write";

    /// <summary>
    /// Scope for reading source synchronization jobs through Asset Service.
    /// </summary>
    public const string SyncJobsReadScope = "sync.jobs.read";

    /// <summary>
    /// Scope for rebuilding Search Service index documents.
    /// </summary>
    public const string SearchIndexRebuildScope = "search.index.rebuild";

    /// <summary>
    /// Scope for querying language assets through Search Service.
    /// </summary>
    public const string SearchQueryScope = "search.query";
}
