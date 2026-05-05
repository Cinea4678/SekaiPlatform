using Microsoft.EntityFrameworkCore;

namespace SekaiPlatform.Database;

/// <summary>
/// EF Core context for tenants, users, story assets, translations, and sync jobs.
/// </summary>
/// <param name="options">The context options used to configure database access.</param>
public sealed class SekaiPlatformDbContext(DbContextOptions<SekaiPlatformDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the tenant set.
    /// </summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>
    /// Gets the user set.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the tenant membership set.
    /// </summary>
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();

    /// <summary>
    /// Gets the linked OAuth identity set.
    /// </summary>
    public DbSet<UserOAuth> UserOAuths => Set<UserOAuth>();

    /// <summary>
    /// Gets the story group set.
    /// </summary>
    public DbSet<StoryGroup> StoryGroups => Set<StoryGroup>();

    /// <summary>
    /// Gets the story set.
    /// </summary>
    public DbSet<Story> Stories => Set<Story>();

    /// <summary>
    /// Gets the shared source line set.
    /// </summary>
    public DbSet<StorySourceLine> StorySourceLines => Set<StorySourceLine>();

    /// <summary>
    /// Gets the tenant translation version set.
    /// </summary>
    public DbSet<TranslationVersion> TranslationVersions => Set<TranslationVersion>();

    /// <summary>
    /// Gets the tenant translation line set.
    /// </summary>
    public DbSet<TranslationLine> TranslationLines => Set<TranslationLine>();

    /// <summary>
    /// Gets the sync job set.
    /// </summary>
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTenants(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureUserTenants(modelBuilder);
        ConfigureUserOAuthes(modelBuilder);
        ConfigureStoryGroups(modelBuilder);
        ConfigureStories(modelBuilder);
        ConfigureStorySourceLines(modelBuilder);
        ConfigureTranslationVersions(modelBuilder);
        ConfigureTranslationLines(modelBuilder);
        ConfigureSyncJobs(modelBuilder);
    }

    /// <summary>
    /// Configures tenant table mapping and unique names.
    /// </summary>
    private static void ConfigureTenants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Id).HasColumnName("id");
            entity.Property(tenant => tenant.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
            entity.Property(tenant => tenant.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
            entity.HasIndex(tenant => tenant.Name).IsUnique();
        });
    }

    /// <summary>
    /// Configures user table mapping and login identifier uniqueness.
    /// </summary>
    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.QqId).HasColumnName("qq_id").HasMaxLength(32);
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(128);
            entity.Property(user => user.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.HasIndex(user => user.QqId).IsUnique();
        });
    }

    /// <summary>
    /// Configures tenant memberships, role and status constraints, and tenant/user relationships.
    /// </summary>
    private static void ConfigureUserTenants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserTenant>(entity =>
        {
            entity.ToTable("user_tenants", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_tenants_role",
                    "role IN ('normal', 'admin', 'super_admin')");
                table.HasCheckConstraint(
                    "CK_user_tenants_status",
                    "status IN ('active', 'disabled', 'deleted')");
            });
            entity.HasKey(userTenant => new { userTenant.TenantId, userTenant.UserId });
            entity.Property(userTenant => userTenant.TenantId).HasColumnName("tenant_id");
            entity.Property(userTenant => userTenant.UserId).HasColumnName("user_id");
            entity.Property(userTenant => userTenant.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
            entity.Property(userTenant => userTenant.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(userTenant => userTenant.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(userTenant => userTenant.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(userTenant => userTenant.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
            entity.HasOne(userTenant => userTenant.Tenant)
                .WithMany(tenant => tenant.UserTenants)
                .HasForeignKey(userTenant => userTenant.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(userTenant => userTenant.User)
                .WithMany(user => user.UserTenants)
                .HasForeignKey(userTenant => userTenant.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures linked OAuth identities and their owning user relationship.
    /// </summary>
    private static void ConfigureUserOAuthes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserOAuth>(entity =>
        {
            entity.ToTable("user_oauthes");
            entity.HasKey(userOAuth => new { userOAuth.OAuthType, userOAuth.OAuthId });
            entity.Property(userOAuth => userOAuth.UserId).HasColumnName("user_id");
            entity.Property(userOAuth => userOAuth.OAuthType).HasColumnName("oauth_type").HasMaxLength(32).IsRequired();
            entity.Property(userOAuth => userOAuth.OAuthId).HasColumnName("oauth_id").HasMaxLength(512).IsRequired();
            entity.HasIndex(userOAuth => userOAuth.UserId);
            entity.HasOne(userOAuth => userOAuth.User)
                .WithMany(user => user.UserOAuths)
                .HasForeignKey(userOAuth => userOAuth.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures story group mapping, story type constraints, and upstream identity uniqueness.
    /// </summary>
    private static void ConfigureStoryGroups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoryGroup>(entity =>
        {
            entity.ToTable("story_groups", table =>
            {
                table.HasCheckConstraint(
                    "CK_story_groups_story_type",
                    "story_type IN ('event_story', 'card_story', 'main_story', 'area_talk', 'special_story')");
            });
            entity.HasKey(storyGroup => storyGroup.Id);
            entity.Property(storyGroup => storyGroup.Id).HasColumnName("id");
            entity.Property(storyGroup => storyGroup.StoryType).HasColumnName("story_type").HasMaxLength(32).IsRequired();
            entity.Property(storyGroup => storyGroup.ExternalType).HasColumnName("external_type").HasMaxLength(32);
            entity.Property(storyGroup => storyGroup.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(storyGroup => storyGroup.DisplayNo).HasColumnName("display_no");
            entity.Property(storyGroup => storyGroup.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(storyGroup => storyGroup.Subtitle).HasColumnName("subtitle").HasMaxLength(255);
            entity.Property(storyGroup => storyGroup.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(storyGroup => storyGroup.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(storyGroup => storyGroup.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(storyGroup => storyGroup.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(storyGroup => new { storyGroup.StoryType, storyGroup.ExternalType, storyGroup.ExternalId })
                .IsUnique()
                .AreNullsDistinct(false);
        });
    }

    /// <summary>
    /// Configures story mapping, story type constraints, and optional group ownership.
    /// </summary>
    private static void ConfigureStories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Story>(entity =>
        {
            entity.ToTable("stories", table =>
            {
                table.HasCheckConstraint(
                    "CK_stories_story_type",
                    "story_type IN ('event_story', 'card_story', 'main_story', 'area_talk', 'special_story')");
            });
            entity.HasKey(story => story.Id);
            entity.Property(story => story.Id).HasColumnName("id");
            entity.Property(story => story.GroupId).HasColumnName("group_id");
            entity.Property(story => story.StoryType).HasColumnName("story_type").HasMaxLength(32).IsRequired();
            entity.Property(story => story.ScenarioId).HasColumnName("scenario_id").HasMaxLength(255).IsRequired();
            entity.Property(story => story.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(story => story.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(story => story.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(story => story.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(story => story.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(story => story.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(story => new { story.StoryType, story.ScenarioId }).IsUnique();
            entity.HasOne(story => story.Group)
                .WithMany(storyGroup => storyGroup.Stories)
                .HasForeignKey(story => story.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures shared source lines and their story-scoped line ordering.
    /// </summary>
    private static void ConfigureStorySourceLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StorySourceLine>(entity =>
        {
            entity.ToTable("story_source_lines", table =>
            {
                table.HasCheckConstraint(
                    "CK_story_source_lines_line_type",
                    "line_type IN ('dialogue', 'scene', 'upper_scene', 'choice', 'separator')");
            });
            entity.HasKey(sourceLine => sourceLine.Id);
            entity.HasAlternateKey(sourceLine => new { sourceLine.Id, sourceLine.StoryId });
            entity.Property(sourceLine => sourceLine.Id).HasColumnName("id");
            entity.Property(sourceLine => sourceLine.StoryId).HasColumnName("story_id");
            entity.Property(sourceLine => sourceLine.LineNo).HasColumnName("line_no").IsRequired();
            entity.Property(sourceLine => sourceLine.LineType).HasColumnName("line_type").HasMaxLength(32).IsRequired();
            entity.Property(sourceLine => sourceLine.Speaker).HasColumnName("speaker").HasMaxLength(128);
            entity.Property(sourceLine => sourceLine.Text).HasColumnName("text").IsRequired();
            entity.Property(sourceLine => sourceLine.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(sourceLine => sourceLine.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(sourceLine => sourceLine.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.HasIndex(sourceLine => new { sourceLine.StoryId, sourceLine.LineNo }).IsUnique();
            entity.HasOne(sourceLine => sourceLine.Story)
                .WithMany(story => story.SourceLines)
                .HasForeignKey(sourceLine => sourceLine.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures tenant translation versions and creator membership integrity.
    /// </summary>
    private static void ConfigureTranslationVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranslationVersion>(entity =>
        {
            entity.ToTable("translation_versions");
            entity.HasKey(version => version.Id);
            entity.HasAlternateKey(version => new { version.Id, version.StoryId });
            entity.Property(version => version.Id).HasColumnName("id");
            entity.Property(version => version.TenantId).HasColumnName("tenant_id");
            entity.Property(version => version.StoryId).HasColumnName("story_id");
            entity.Property(version => version.VersionNo).HasColumnName("version_no").IsRequired();
            entity.Property(version => version.Title).HasColumnName("title").HasMaxLength(255);
            entity.Property(version => version.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(version => version.CreatedBy).HasColumnName("created_by");
            entity.Property(version => version.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(version => version.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(version => version.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(version => new { version.TenantId, version.StoryId, version.VersionNo }).IsUnique();
            entity.HasOne(version => version.Tenant)
                .WithMany(tenant => tenant.TranslationVersions)
                .HasForeignKey(version => version.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.Story)
                .WithMany(story => story.TranslationVersions)
                .HasForeignKey(version => version.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.Creator)
                .WithMany(user => user.CreatedTranslationVersions)
                .HasForeignKey(version => version.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.CreatorMembership)
                .WithMany()
                .HasForeignKey(version => new { version.TenantId, version.CreatedBy })
                .HasPrincipalKey(userTenant => new { userTenant.TenantId, userTenant.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures translated lines and enforces mapping to the same story as their version and source line.
    /// </summary>
    private static void ConfigureTranslationLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranslationLine>(entity =>
        {
            entity.ToTable("translation_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.VersionId).HasColumnName("version_id");
            entity.Property(line => line.SourceLineId).HasColumnName("source_line_id");
            entity.Property(line => line.StoryId).HasColumnName("story_id");
            entity.Property(line => line.LineNo).HasColumnName("line_no").IsRequired();
            entity.Property(line => line.Speaker).HasColumnName("speaker").HasMaxLength(128);
            entity.Property(line => line.Text).HasColumnName("text").IsRequired();
            entity.Property(line => line.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(line => line.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(line => line.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.HasIndex(line => new { line.VersionId, line.LineNo }).IsUnique();
            entity.HasIndex(line => new { line.VersionId, line.SourceLineId }).IsUnique();
            entity.HasOne(line => line.Version)
                .WithMany(version => version.Lines)
                .HasForeignKey(line => new { line.VersionId, line.StoryId })
                .HasPrincipalKey(version => new { version.Id, version.StoryId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(line => line.SourceLine)
                .WithMany(sourceLine => sourceLine.TranslationLines)
                .HasForeignKey(line => new { line.SourceLineId, line.StoryId })
                .HasPrincipalKey(sourceLine => new { sourceLine.Id, sourceLine.StoryId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures sync job mapping and allowed trigger/status values.
    /// </summary>
    private static void ConfigureSyncJobs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncJob>(entity =>
        {
            entity.ToTable("sync_jobs", table =>
            {
                table.HasCheckConstraint(
                    "CK_sync_jobs_trigger_type",
                    "trigger_type IN ('manual', 'scheduled')");
                table.HasCheckConstraint(
                    "CK_sync_jobs_status",
                    "status IN ('pending', 'running', 'succeeded', 'failed')");
            });
            entity.HasKey(syncJob => syncJob.Id);
            entity.Property(syncJob => syncJob.Id).HasColumnName("id");
            entity.Property(syncJob => syncJob.JobType).HasColumnName("job_type").HasMaxLength(64).IsRequired();
            entity.Property(syncJob => syncJob.TriggerType).HasColumnName("trigger_type").HasMaxLength(32).IsRequired();
            entity.Property(syncJob => syncJob.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(syncJob => syncJob.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
            entity.Property(syncJob => syncJob.EndedAt).HasColumnName("ended_at").HasColumnType("timestamp with time zone");
            entity.Property(syncJob => syncJob.ErrorMessage).HasColumnName("error_message");
            entity.Property(syncJob => syncJob.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(syncJob => syncJob.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(syncJob => syncJob.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.HasIndex(syncJob => new { syncJob.Status, syncJob.CreatedAt });
        });
    }
}
