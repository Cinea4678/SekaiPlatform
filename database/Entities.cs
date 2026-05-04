namespace SekaiPlatform.Database;

/// <summary>
/// Represents a tenant that isolates users and translated assets.
/// </summary>
public sealed class Tenant
{
    /// <summary>
    /// Gets or sets the tenant primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the unique tenant display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional tenant avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets the user memberships scoped to this tenant.
    /// </summary>
    public ICollection<UserTenant> UserTenants { get; } = [];

    /// <summary>
    /// Gets the translation versions owned by this tenant.
    /// </summary>
    public ICollection<TranslationVersion> TranslationVersions { get; } = [];
}

/// <summary>
/// Represents a platform user that may join one or more tenants.
/// </summary>
public sealed class User
{
    /// <summary>
    /// Gets or sets the user primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the optional QQ identifier used for login or linking.
    /// </summary>
    public string? QqId { get; set; }

    /// <summary>
    /// Gets or sets the user-facing display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional user avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the password hash for username/password authentication.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets when the user row was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the user row was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets the tenant memberships for this user.
    /// </summary>
    public ICollection<UserTenant> UserTenants { get; } = [];

    /// <summary>
    /// Gets the OAuth identities linked to this user.
    /// </summary>
    public ICollection<UserOAuth> UserOAuths { get; } = [];

    /// <summary>
    /// Gets the translation versions created by this user.
    /// </summary>
    public ICollection<TranslationVersion> CreatedTranslationVersions { get; } = [];
}

/// <summary>
/// Represents a user's role and lifecycle state within a tenant.
/// </summary>
public sealed class UserTenant
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant role, such as normal, admin, or super admin.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the membership status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the membership was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the membership was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the membership was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the tenant navigation.
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Gets or sets the user navigation.
    /// </summary>
    public User? User { get; set; }
}

/// <summary>
/// Represents an external OAuth identity linked to a platform user.
/// </summary>
public sealed class UserOAuth
{
    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth provider type.
    /// </summary>
    public string OAuthType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific OAuth subject identifier.
    /// </summary>
    public string OAuthId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning user navigation.
    /// </summary>
    public User? User { get; set; }
}

/// <summary>
/// Represents a navigable collection of stories from a source category.
/// </summary>
public sealed class StoryGroup
{
    /// <summary>
    /// Gets or sets the story group primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the normalized platform story type.
    /// </summary>
    public string StoryType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upstream source type when available.
    /// </summary>
    public string? ExternalType { get; set; }

    /// <summary>
    /// Gets or sets the upstream group identifier when available.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the optional display number from the upstream source.
    /// </summary>
    public int? DisplayNo { get; set; }

    /// <summary>
    /// Gets or sets the story group title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional story group subtitle.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets source-specific metadata stored as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the story group was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the story group was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the story group was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets the stories contained in this group.
    /// </summary>
    public ICollection<Story> Stories { get; } = [];
}

/// <summary>
/// Represents a single source story that can be searched and translated.
/// </summary>
public sealed class Story
{
    /// <summary>
    /// Gets or sets the story primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the optional parent story group identifier.
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the normalized platform story type.
    /// </summary>
    public string StoryType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upstream scenario identifier.
    /// </summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the story title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order within its story group.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets source-specific metadata stored as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the story was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the story was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the story was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the parent story group navigation.
    /// </summary>
    public StoryGroup? Group { get; set; }

    /// <summary>
    /// Gets the shared source lines for this story.
    /// </summary>
    public ICollection<StorySourceLine> SourceLines { get; } = [];

    /// <summary>
    /// Gets the tenant-owned translation versions for this story.
    /// </summary>
    public ICollection<TranslationVersion> TranslationVersions { get; } = [];
}

/// <summary>
/// Represents a shared original text line in a story.
/// </summary>
public sealed class StorySourceLine
{
    /// <summary>
    /// Gets or sets the source line primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owning story identifier.
    /// </summary>
    public long StoryId { get; set; }

    /// <summary>
    /// Gets or sets the line number within the story.
    /// </summary>
    public int LineNo { get; set; }

    /// <summary>
    /// Gets or sets the source line type.
    /// </summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional speaker name.
    /// </summary>
    public string? Speaker { get; set; }

    /// <summary>
    /// Gets or sets the original line text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets source-specific metadata stored as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the source line was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the source line was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning story navigation.
    /// </summary>
    public Story? Story { get; set; }

    /// <summary>
    /// Gets the tenant translation lines mapped to this source line.
    /// </summary>
    public ICollection<TranslationLine> TranslationLines { get; } = [];
}

/// <summary>
/// Represents a tenant-owned translation version for a story.
/// </summary>
public sealed class TranslationVersion
{
    /// <summary>
    /// Gets or sets the translation version primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owning tenant identifier.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Gets or sets the translated story identifier.
    /// </summary>
    public long StoryId { get; set; }

    /// <summary>
    /// Gets or sets the tenant-local version number for the story.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the optional translation version title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who created this version.
    /// </summary>
    public long CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when the translation version was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the translation version was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the translation version was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning tenant navigation.
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Gets or sets the translated story navigation.
    /// </summary>
    public Story? Story { get; set; }

    /// <summary>
    /// Gets or sets the creator user navigation.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the creator's tenant membership navigation.
    /// </summary>
    public UserTenant? CreatorMembership { get; set; }

    /// <summary>
    /// Gets the translated lines in this version.
    /// </summary>
    public ICollection<TranslationLine> Lines { get; } = [];
}

/// <summary>
/// Represents one translated line within a tenant translation version.
/// </summary>
public sealed class TranslationLine
{
    /// <summary>
    /// Gets or sets the translation line primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owning translation version identifier.
    /// </summary>
    public long VersionId { get; set; }

    /// <summary>
    /// Gets or sets the source line identifier this translation maps to.
    /// </summary>
    public long SourceLineId { get; set; }

    /// <summary>
    /// Gets or sets the story identifier shared with the version and source line.
    /// </summary>
    public long StoryId { get; set; }

    /// <summary>
    /// Gets or sets the line number within the story.
    /// </summary>
    public int LineNo { get; set; }

    /// <summary>
    /// Gets or sets the optional translated speaker name.
    /// </summary>
    public string? Speaker { get; set; }

    /// <summary>
    /// Gets or sets the translated line text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets translation-specific metadata stored as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the translation line was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the translation line was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning translation version navigation.
    /// </summary>
    public TranslationVersion? Version { get; set; }

    /// <summary>
    /// Gets or sets the mapped source line navigation.
    /// </summary>
    public StorySourceLine? SourceLine { get; set; }
}

/// <summary>
/// Represents an external source synchronization job and its outcome.
/// </summary>
public sealed class SyncJob
{
    /// <summary>
    /// Gets or sets the sync job primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the sync job category.
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the sync job was triggered.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current sync job status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the sync job started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the sync job ended.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Gets or sets the failure message when the job did not succeed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets sync-specific metadata stored as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the sync job row was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the sync job row was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
