namespace SekaiPlatform.Database;

public sealed class Tenant
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    public ICollection<UserTenant> UserTenants { get; } = [];
    public ICollection<TranslationVersion> TranslationVersions { get; } = [];
}

public sealed class User
{
    public long Id { get; set; }
    public string? QqId { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserTenant> UserTenants { get; } = [];
    public ICollection<UserOAuth> UserOAuths { get; } = [];
    public ICollection<TranslationVersion> CreatedTranslationVersions { get; } = [];
}

public sealed class UserTenant
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}

public sealed class UserOAuth
{
    public long UserId { get; set; }
    public string OAuthType { get; set; } = string.Empty;
    public string OAuthId { get; set; } = string.Empty;

    public User? User { get; set; }
}

public sealed class StoryGroup
{
    public long Id { get; set; }
    public string StoryType { get; set; } = string.Empty;
    public string? ExternalType { get; set; }
    public string? ExternalId { get; set; }
    public int? DisplayNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Story> Stories { get; } = [];
}

public sealed class Story
{
    public long Id { get; set; }
    public long? GroupId { get; set; }
    public string StoryType { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public StoryGroup? Group { get; set; }
    public ICollection<StorySourceLine> SourceLines { get; } = [];
    public ICollection<TranslationVersion> TranslationVersions { get; } = [];
}

public sealed class StorySourceLine
{
    public long Id { get; set; }
    public long StoryId { get; set; }
    public int LineNo { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string? Speaker { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Story? Story { get; set; }
    public ICollection<TranslationLine> TranslationLines { get; } = [];
}

public sealed class TranslationVersion
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long StoryId { get; set; }
    public int VersionNo { get; set; }
    public string? Title { get; set; }
    public long CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Story? Story { get; set; }
    public User? Creator { get; set; }
    public UserTenant? CreatorMembership { get; set; }
    public ICollection<TranslationLine> Lines { get; } = [];
}

public sealed class TranslationLine
{
    public long Id { get; set; }
    public long VersionId { get; set; }
    public long SourceLineId { get; set; }
    public long StoryId { get; set; }
    public int LineNo { get; set; }
    public string? Speaker { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public TranslationVersion? Version { get; set; }
    public StorySourceLine? SourceLine { get; set; }
}

public sealed class SyncJob
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
