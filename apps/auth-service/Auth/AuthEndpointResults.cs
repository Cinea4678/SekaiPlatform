using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;

/// <summary>
/// Builds shared Auth Service endpoint result shapes.
/// </summary>
internal static class AuthEndpointResults
{
    /// <summary>
    /// Creates a trace-aware error response for authentication endpoints.
    /// </summary>
    public static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
    }
}
