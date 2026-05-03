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

public static class SekaiWebApplicationBuilderExtensions
{
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

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);
                options.Events = CreateJwtBearerEvents();
            });
        builder.Services.AddAuthorization();

        return builder;
    }

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
