using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SekaiPlatform.Shared.Web.Auth;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Provides process-local internal token keys and configuration for integration tests.
/// </summary>
internal static class IntegrationTestInternalAuth
{
    private const string Issuer = "sekai-platform-internal";
    private static readonly Lazy<ActorKey> ApiService = new(CreateActorKey);
    private static readonly Lazy<ActorKey> AssetService = new(CreateActorKey);
    private static readonly Lazy<ActorKey> SyncWorker = new(CreateActorKey);

    /// <summary>
    /// Adds internal token configuration for a test host.
    /// </summary>
    public static void AddConfiguration(
        IDictionary<string, string?> configuration,
        string actor,
        bool includePrivateKey = false)
    {
        configuration["InternalAuth:Issuer"] = Issuer;
        configuration["InternalAuth:Actor"] = actor;
        configuration["InternalAuth:PublicKeys:api-service"] = ApiService.Value.PublicKey;
        configuration["InternalAuth:PublicKeys:asset-service"] = AssetService.Value.PublicKey;
        configuration["InternalAuth:PublicKeys:sync-worker"] = SyncWorker.Value.PublicKey;

        if (includePrivateKey)
        {
            configuration["InternalAuth:PrivateKeyPkcs8"] = GetActorKey(actor).PrivateKey;
        }
    }

    /// <summary>
    /// Issues an internal token from the requested actor.
    /// </summary>
    public static string Issue(
        string actor,
        string audience,
        string scope,
        long? subjectUserId = null,
        long? tenantId = null)
    {
        return IssueWithSigningActor(
            actor,
            actor,
            audience,
            scope,
            subjectUserId,
            tenantId);
    }

    /// <summary>
    /// Issues a token whose actor claim differs from the private key used to sign it.
    /// </summary>
    public static string IssueWithSigningActor(
        string signingActor,
        string tokenActor,
        string audience,
        string scope,
        long? subjectUserId = null,
        long? tenantId = null)
    {
        var issuer = new SekaiInternalTokenIssuer(Options.Create(new SekaiInternalAuthOptions
        {
            Issuer = Issuer,
            Actor = tokenActor,
            PrivateKeyPkcs8 = GetActorKey(signingActor).PrivateKey,
            PublicKeys = new Dictionary<string, string>
            {
                [SekaiInternalAuthDefaults.ApiServiceActor] = ApiService.Value.PublicKey,
                [SekaiInternalAuthDefaults.AssetServiceActor] = AssetService.Value.PublicKey,
                [SekaiInternalAuthDefaults.SyncWorkerActor] = SyncWorker.Value.PublicKey
            }
        }));

        return issuer.Issue(audience, scope, subjectUserId, tenantId);
    }

    private static ActorKey GetActorKey(string actor)
    {
        return actor switch
        {
            SekaiInternalAuthDefaults.ApiServiceActor => ApiService.Value,
            SekaiInternalAuthDefaults.AssetServiceActor => AssetService.Value,
            SekaiInternalAuthDefaults.SyncWorkerActor => SyncWorker.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "No integration-test key for actor.")
        };
    }

    private static ActorKey CreateActorKey()
    {
        using var rsa = RSA.Create(2048);
        return new ActorKey(
            Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
    }

    private sealed record ActorKey(string PrivateKey, string PublicKey);
}
