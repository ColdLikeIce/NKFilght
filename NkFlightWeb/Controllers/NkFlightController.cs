using Microsoft.AspNetCore.Mvc;
using NkFlightWeb.Impl;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Controllers
{
    public class NkFlightController : BaseController
    {
        private readonly INkFlightDomain _domain;

        public NkFlightController(INkFlightDomain domain)
        {
            _domain = domain;
        }

        /// <summary>
        ///  报价接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("SearchAirtickets")]
        public async Task<SearchAirtickets_Data> SearchAirtickets([FromBody] SearchAirticketsInput dto)
        {
            return await _domain.SearchAirtickets(dto);
        }

        /// <summary>
        ///  确认接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("Verification")]
        public async Task<Verification_Data> Verification([FromBody] VerificationInput dto)
        {
            return await _domain.Verification(dto);
        }

        /// <summary>
        ///  确认接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("CreateOrder")]
        public async Task<CreateOrder_Data> CreateOrder([FromBody] CreateOrderInput dto)
        {
            return await _domain.CreateOrder(dto);
        }

        /// <summary>
        ///  取消订单接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("CancelOrder")]
        public async Task<CancelOrde_Data> CancelOrder([FromBody] CancelOrderInput dto)
        {
            return await _domain.CancelOrder(dto);
        }

        /// <summary>
        ///  查询详情接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("QueryOrder")]
        public async Task<QueryOrder_Data> QueryOrder([FromBody] QueryOrderInput dto)
        {
            return await _domain.QueryOrder(dto);
        }

        /// <summary>
        ///  查询详情接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("PayVerification")]
        public async Task<PayVerification_Data> PayVerification([FromBody] PayVerificationInput dto)
        {
            return await _domain.PayVerification(dto);
        }
    }
}