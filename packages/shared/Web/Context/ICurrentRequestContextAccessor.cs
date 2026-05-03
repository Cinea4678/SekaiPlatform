namespace SekaiPlatform.Shared.Web;

public interface ICurrentRequestContextAccessor
{
    CurrentRequestContext GetCurrent();
}
