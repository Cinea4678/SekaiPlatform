using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Http;
using SekaiPlatform.Shared.Web.Responses;
using SekaiPlatform.Shared.Web.Search;

namespace SekaiPlatform.Shared.Web.Hosting;

/// <summary>
/// Provides shared ASP.NET Core hosting defaults for Sekai Platform services.
/// </summary>
public static class SekaiWebApplicationBuilderExtensions
{
    /// <summary>
    /// Minimum UTF-8 byte length required for symmetric JWT signing keys.
    /// </summary>
    private const int MinimumSigningKeyBytes = 32;

    /// <summary>
    /// Adds shared logging, request context, health checks, authentication, and authorization defaults.
    /// </summary>
    /// <param name="builder">The web application builder to configure.</param>
    /// <param name="authenticationMode">The token model used by this web service.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddSekaiPlatformWebDefaults(
        this WebApplicationBuilder builder,
        SekaiAuthenticationMode authenticationMode = SekaiAuthenticationMode.ExternalJwt,
        bool requireInternalTokenIssuer = false)
    {
        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentRequestContextAccessor, HttpCurrentRequestContextAccessor>();
        builder.Services.AddTransient<SekaiContextPropagationHandler>();
        builder.Services.AddHealthChecks();
        builder.Services.Configure<SekaiJwtOptions>(
            builder.Configuration.GetSection(SekaiJwtOptions.SectionName));
        builder.Services.AddSekaiPlatformInternalTokenIssuer(
            builder.Configuration,
            requirePrivateKey: requireInternalTokenIssuer);

        if (authenticationMode == SekaiAuthenticationMode.ExternalJwt)
        {
            AddExternalJwtAuthentication(builder);
        }
        else if (authenticationMode == SekaiAuthenticationMode.InternalToken)
        {
            AddInternalTokenAuthentication(builder);
        }

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                SekaiAuthorizationPolicies.LoggedIn,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(SekaiAuthDefaults.UserIdClaimType));
            options.AddPolicy(
                SekaiAuthorizationPolicies.TenantSelected,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(SekaiAuthDefaults.UserIdClaimType)
                    .RequireClaim(SekaiAuthDefaults.TenantIdClaimType));
        });

        return builder;
    }

    /// <summary>
    /// Adds the internal token issuer used by services that call other internal services.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Application configuration containing internal auth settings.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSekaiPlatformInternalTokenIssuer(
        this IServiceCollection services,
        IConfiguration configuration,
        bool requirePrivateKey = false)
    {
        var options = services
            .AddOptions<SekaiInternalAuthOptions>()
            .Bind(configuration.GetSection(SekaiInternalAuthOptions.SectionName));
        if (requirePrivateKey)
        {
            options
                .Validate(
                    HasValidInternalTokenIssuerOptions,
                    "Invalid internal token issuer configuration.")
                .ValidateOnStart();
        }

        services.AddSingleton<SekaiInternalTokenIssuer>();
        return services;
    }

    /// <summary>
    /// Adds an internal HTTP client that propagates platform request context headers.
    /// </summary>
    /// <param name="services">The service collection to register the HTTP client into.</param>
    /// <param name="name">The named HTTP client identifier.</param>
    /// <param name="configuration">The application configuration containing internal service addresses.</param>
    /// <param name="configurationKey">The key under <c>InternalServices</c> containing the base address.</param>
    /// <returns>The HTTP client builder for additional configuration.</returns>
    public static IHttpClientBuilder AddSekaiPlatformInternalHttpClient(
        this IServiceCollection services,
        string name,
        IConfiguration configuration,
        string configurationKey)
    {
        var baseAddress = configuration[$"InternalServices:{configurationKey}"];
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new InvalidOperationException($"Missing InternalServices:{configurationKey} configuration.");
        }

        return services
            .AddHttpClient(name, client => client.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<SekaiContextPropagationHandler>();
    }

    /// <summary>
    /// Adds the typed client used to request Search Service index refresh operations.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="configuration">The application configuration containing Search Service and token settings.</param>
    /// <returns>The HTTP client builder for additional configuration.</returns>
    public static IHttpClientBuilder AddSekaiPlatformSearchIndexRefreshClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseAddress = configuration["InternalServices:SearchService"];
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new InvalidOperationException("Missing InternalServices:SearchService configuration.");
        }

        var refreshOptions = SearchIndexRefreshOptions.FromConfiguration(
            configuration.GetSection(SearchIndexRefreshOptions.SectionName));
        services.AddSingleton(refreshOptions);

        return services
            .AddHttpClient<SearchIndexRefreshClient>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.Timeout = refreshOptions.RequestTimeout;
            })
            .AddHttpMessageHandler<SekaiContextPropagationHandler>();
    }

    /// <summary>
    /// Adds shared request tracing, error handling, authentication, and authorization middleware.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <param name="useAuthentication">Whether to add authentication and authorization middleware.</param>
    /// <param name="writeUnhandledExceptionAsync">Optional service-specific unhandled exception response writer.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication UseSekaiPlatformWebDefaults(
        this WebApplication app,
        bool useAuthentication = true,
        Func<HttpContext, Exception, Task>? writeUnhandledExceptionAsync = null)
    {
        app.Use(async (httpContext, next) =>
        {
            var contextAccessor = httpContext.RequestServices.GetRequiredService<ICurrentRequestContextAccessor>();
            var requestContext = contextAccessor.GetCurrent();
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SekaiPlatform.Request");

            httpContext.Response.OnStarting(() =>
            {
                httpContext.Response.Headers[SekaiHeaders.TraceId] = requestContext.TraceId;
                return Task.CompletedTask;
            });

            using var scope = logger.BeginScope(
                "trace_id:{TraceId} user_id:{UserId} tenant_id:{TenantId}",
                requestContext.TraceId,
                requestContext.UserId,
                requestContext.TenantId);

            try
            {
                await next();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled request exception.");

                if (httpContext.Response.HasStarted)
                {
                    throw;
                }

                httpContext.Response.Clear();
                if (writeUnhandledExceptionAsync is not null)
                {
                    await writeUnhandledExceptionAsync(httpContext, exception);
                }
                else
                {
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpContext.Response.WriteAsJsonAsync(
                        new ErrorResponse("服务器内部错误。", requestContext.TraceId),
                        httpContext.RequestAborted);
                }
            }
        });

        if (useAuthentication)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        return app;
    }

    private static void AddExternalJwtAuthentication(WebApplicationBuilder builder)
    {
        var jwtOptions = builder.Configuration
            .GetSection(SekaiJwtOptions.SectionName)
            .Get<SekaiJwtOptions>() ?? new SekaiJwtOptions();
        ValidateJwtOptions(jwtOptions);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateExternalTokenValidationParameters(jwtOptions);
                options.Events = CreateExternalJwtBearerEvents();
            });
    }

    private static void AddInternalTokenAuthentication(WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = CreateInternalJwtBearerEvents();
            });
        builder.Services.Configure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                var internalOptions = builder.Configuration
                    .GetSection(SekaiInternalAuthOptions.SectionName)
                    .Get<SekaiInternalAuthOptions>() ?? new SekaiInternalAuthOptions();
                ValidateInternalAuthOptions(internalOptions);
                options.TokenValidationParameters = CreateInternalTokenValidationParameters(internalOptions);
            });
    }

    /// <summary>
    /// Creates token validation parameters from the configured external JWT options.
    /// </summary>
    private static TokenValidationParameters CreateExternalTokenValidationParameters(SekaiJwtOptions options)
    {
        var signingKey = Encoding.UTF8.GetBytes(options.SigningKey);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Creates token validation parameters from the configured internal token options.
    /// </summary>
    private static TokenValidationParameters CreateInternalTokenValidationParameters(SekaiInternalAuthOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Actor,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = CreateInternalSigningKeyResolver(options),
            TryAllIssuerSigningKeys = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    private static IssuerSigningKeyResolver CreateInternalSigningKeyResolver(SekaiInternalAuthOptions options)
    {
        var signingKeys = CreateInternalSigningKeys(options);
        return (_, _, keyId, _) =>
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                return [];
            }

            return signingKeys.TryGetValue(keyId, out var key)
                ? [key]
                : [];
        };
    }

    private static IReadOnlyDictionary<string, SecurityKey> CreateInternalSigningKeys(SekaiInternalAuthOptions options)
    {
        var signingKeys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);
        foreach (var (actor, publicKey) in options.PublicKeys)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            signingKeys[actor] = new RsaSecurityKey(rsa) { KeyId = actor };
        }

        return signingKeys;
    }

    /// <summary>
    /// Validates JWT settings required by shared authentication defaults.
    /// </summary>
    private static void ValidateJwtOptions(SekaiJwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("Jwt:Issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Jwt:Audience must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            errors.Add("Jwt:SigningKey must be configured.");
        }
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyBytes)
        {
            errors.Add($"Jwt:SigningKey must be at least {MinimumSigningKeyBytes} UTF-8 bytes.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid JWT configuration: " + string.Join(" ", errors));
        }
    }

    /// <summary>
    /// Validates internal token settings required by internal services.
    /// </summary>
    private static void ValidateInternalAuthOptions(SekaiInternalAuthOptions options)
    {
        var errors = new List<string>();

        ValidateInternalAuthIdentity(options, errors);
        ValidateInternalPublicKeys(options, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid internal auth configuration: " + string.Join(" ", errors));
        }
    }

    private static void ValidateInternalTokenIssuerOptions(
        SekaiInternalAuthOptions options,
        bool requirePrivateKey)
    {
        if (!requirePrivateKey)
        {
            return;
        }

        var errors = new List<string>();
        ValidateInternalAuthIdentity(options, errors);

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPkcs8))
        {
            errors.Add("InternalAuth:PrivateKeyPkcs8 must be configured for services that issue internal tokens.");
        }
        else
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.PrivateKeyPkcs8), out _);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                errors.Add("InternalAuth:PrivateKeyPkcs8 must be a base64 PKCS#8 RSA private key.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid internal token issuer configuration: " + string.Join(" ", errors));
        }
    }

    private static bool HasValidInternalTokenIssuerOptions(SekaiInternalAuthOptions options)
    {
        try
        {
            ValidateInternalTokenIssuerOptions(options, requirePrivateKey: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ValidateInternalAuthIdentity(SekaiInternalAuthOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("InternalAuth:Issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Actor))
        {
            errors.Add("InternalAuth:Actor must be configured.");
        }
    }

    private static void ValidateInternalPublicKeys(SekaiInternalAuthOptions options, List<string> errors)
    {
        var validPublicKeyCount = 0;
        foreach (var (actor, publicKey) in options.PublicKeys)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                continue;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                validPublicKeyCount++;
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                errors.Add($"InternalAuth:PublicKeys:{actor} must be a base64 SubjectPublicKeyInfo RSA public key.");
            }
        }

        if (validPublicKeyCount == 0)
        {
            errors.Add("InternalAuth:PublicKeys must contain at least one actor public key.");
        }
    }

    /// <summary>
    /// Creates external JWT bearer events that read auth cookies and return standard error payloads.
    /// </summary>
    private static JwtBearerEvents CreateExternalJwtBearerEvents()
    {
        return new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token)
                    && context.Request.Cookies.TryGetValue(SekaiAuthDefaults.AuthenticationCookieName, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteAuthenticationErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "未登录或登录已失效。");
            },
            OnForbidden = context =>
            {
                return WriteAuthenticationErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "无权访问。");
            }
        };
    }

    /// <summary>
    /// Creates internal JWT bearer events that verify actor and signing key identity.
    /// </summary>
    private static JwtBearerEvents CreateInternalJwtBearerEvents()
    {
        return new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var actor = context.Principal?.FindClaimValue(SekaiInternalAuthDefaults.ActorClaimType);
                var keyId = context.SecurityToken.SigningKey?.KeyId;
                if (string.IsNullOrWhiteSpace(actor)
                    || string.IsNullOrWhiteSpace(keyId)
                    || !string.Equals(actor, keyId, StringComparison.Ordinal))
                {
                    context.Fail("Internal token actor does not match signing key.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteAuthenticationErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "未登录或登录已失效。");
            },
            OnForbidden = context =>
            {
                return WriteAuthenticationErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "无权访问。");
            }
        };
    }

    /// <summary>
    /// Writes a standard authentication or authorization error response when possible.
    /// </summary>
    private static async Task WriteAuthenticationErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string message)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var contextAccessor = httpContext.RequestServices.GetRequiredService<ICurrentRequestContextAccessor>();
        var requestContext = contextAccessor.GetCurrent();

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(message, requestContext.TraceId),
            httpContext.RequestAborted);
    }
}
