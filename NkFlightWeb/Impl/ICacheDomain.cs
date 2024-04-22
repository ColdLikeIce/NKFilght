using CommonCore.Dependency;

namespace NkFlightWeb.Impl
{
    public interface ICacheDomain : IScopedDependency
    {
        Task<string> GetValueParameter(string key);
    }
}