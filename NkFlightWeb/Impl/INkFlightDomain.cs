using CommonCore.Dependency;
using NkFlightWeb.Service.Dto;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Impl
{
    public interface INkFlightDomain : IScopedDependency
    {
        Task<bool> GetToken();

        /// <summary>
        /// 构建城市
        /// </summary>
        /// <returns></returns>
        Task BuildCity();

        Task<bool> PushAllFlightToDb(SearchDayDto dto);

        Task<SearchAirtickets_Data> SearchAirtickets(SearchAirticketsInput dto);
    }
}