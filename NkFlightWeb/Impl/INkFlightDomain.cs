using CommonCore.Dependency;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Impl
{
    public interface INkFlightDomain : IScopedDependency
    {
        Task GetToken();

        Task<SearchAirtickets_Data> SearchAirtickets(SearchAirticketsInput dto);
    }
}