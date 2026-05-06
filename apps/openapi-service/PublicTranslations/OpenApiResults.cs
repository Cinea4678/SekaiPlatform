using System.Net;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Produces public Open API responses and maps internal service failures.
/// </summary>
internal static class OpenApiResults
{
    /// <summary>
    /// Forwards successful internal JSON and converts failures to the public error envelope.
    /// </summary>
    public static async Task<IResult> FromInternalResponseAsync(
        HttpResponseMessage response,
        ICurrentRequestContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return Results.Content(body, contentType, statusCode: (int)response.StatusCode);
        }

        return Error(
            contextAccessor,
            ToPublicStatusCode(response.StatusCode),
            ToErrorCode(response.StatusCode),
            ToErrorMessage(response.StatusCode));
    }

    /// <summary>
    /// Creates a trace-aware public Open API error result.
    /// </summary>
    public static IResult Error(
        ICurrentRequestContextAccessor contextAccessor,
        int statusCode,
        string code,
        string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(
            new OpenApiErrorResponse(code, message, requestContext.TraceId),
            statusCode: statusCode);
    }

    /// <summary>
    /// Writes the Open API error envelope for unhandled service exceptions.
    /// </summary>
    public static async Task WriteUnhandledExceptionAsync(HttpContext httpContext, Exception _)
    {
        var contextAccessor = httpContext.RequestServices.GetRequiredService<ICurrentRequestContextAccessor>();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new OpenApiErrorResponse("internal_error", "Internal error", contextAccessor.GetCurrent().TraceId),
            httpContext.RequestAborted);
    }

    private static int ToPublicStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            HttpStatusCode.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string ToErrorCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "bad_request",
            HttpStatusCode.NotFound => "not_found",
            HttpStatusCode.TooManyRequests => "rate_limited",
            _ => "internal_error"
        };
    }

    private static string ToErrorMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad request",
            HttpStatusCode.NotFound => "Not found",
            HttpStatusCode.TooManyRequests => "Too many requests",
            _ => "Internal error"
        };
    }
}
