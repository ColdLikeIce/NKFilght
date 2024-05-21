using CommonCore.Dependency;
using NkFlightWeb.Service.Dto;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Impl
{
    public interface INkFlightDomain : IScopedDependency
    {
        Task<bool> GetToken();

        Task<SearchAirtickets_Data> SearchAirtickets(SearchAirticketsInput dto);

        Task<Verification_Data> Verification(VerificationInput dto);

        Task<CreateOrder_Data> CreateOrder(CreateOrderInput dto);

        Task<CancelOrde_Data> CancelOrder(CancelOrderInput dto);

        Task<QueryOrder_Data> QueryOrder(QueryOrderInput dto);

        Task<PayVerification_Data> PayVerification(PayVerificationInput dto);

        /// <summary>
        /// 构建城市
        /// </summary>
        /// <returns></returns>
        Task<string> BuildCity();

        Task<bool> PushAllFlightToDb(SearchDayDto dto);
    }
}