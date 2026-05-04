using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Propagates request trace context to internal HTTP calls.
/// </summary>
/// <param name="serviceProvider">The request service provider used to resolve context accessors.</param>
public sealed class SekaiContextPropagationHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    /// <summary>
    /// Adds platform context headers before sending the outgoing request.
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="cancellationToken">The token used to cancel the send operation.</param>
    /// <returns>The HTTP response from the next handler.</returns>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contextAccessor = serviceProvider.GetService<ICurrentRequestContextAccessor>();
        var context = contextAccessor?.GetCurrent();

        if (context is not null)
        {
            SetHeader(request.Headers, SekaiHeaders.TraceId, context.TraceId);
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Replaces a request header without applying strict value validation.
    /// </summary>
    private static void SetHeader(HttpRequestHeaders headers, string name, string value)
    {
        headers.Remove(name);
        headers.TryAddWithoutValidation(name, value);
    }
}
