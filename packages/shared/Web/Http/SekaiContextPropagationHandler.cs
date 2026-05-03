using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace SekaiPlatform.Shared.Web;

public sealed class SekaiContextPropagationHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contextAccessor = serviceProvider.GetService<ICurrentRequestContextAccessor>();
        var context = contextAccessor?.GetCurrent();

        if (context is not null)
        {
            SetHeader(request.Headers, SekaiHeaders.TraceId, context.TraceId);

            if (context.UserId is not null)
            {
                SetHeader(request.Headers, SekaiHeaders.UserId, context.UserId.Value.ToString());
            }

            if (context.TenantId is not null)
            {
                SetHeader(request.Headers, SekaiHeaders.TenantId, context.TenantId.Value.ToString());
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetHeader(HttpRequestHeaders headers, string name, string value)
    {
        headers.Remove(name);
        headers.TryAddWithoutValidation(name, value);
    }
}
