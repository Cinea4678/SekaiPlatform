using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SekaiPlatform.Shared.Web;

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
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddSekaiPlatformWebDefaults(this WebApplicationBuilder builder)
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

        var jwtOptions = builder.Configuration
            .GetSection(SekaiJwtOptions.SectionName)
            .Get<SekaiJwtOptions>() ?? new SekaiJwtOptions();
        ValidateJwtOptions(jwtOptions);
        builder.Services.Configure<SekaiJwtOptions>(
            builder.Configuration.GetSection(SekaiJwtOptions.SectionName));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);
                options.Events = CreateJwtBearerEvents();
            });
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

        services.Configure<SearchIndexMaintenanceOptions>(
            configuration.GetSection(SearchIndexMaintenanceOptions.SectionName));

        return services.AddHttpClient<SearchIndexRefreshClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    /// <summary>
    /// Adds shared request tracing, error handling, authentication, and authorization middleware.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication UseSekaiPlatformWebDefaults(this WebApplication app)
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
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Internal server error.", requestContext.TraceId),
                    httpContext.RequestAborted);
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Creates token validation parameters from the configured JWT options.
    /// </summary>
    private static TokenValidationParameters CreateTokenValidationParameters(SekaiJwtOptions options)
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
    /// Creates JWT bearer events that read auth cookies and return standard error payloads.
    /// </summary>
    private static JwtBearerEvents CreateJwtBearerEvents()
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
                    "Unauthorized.");
            },
            OnForbidden = context =>
            {
                return WriteAuthenticationErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Forbidden.");
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
