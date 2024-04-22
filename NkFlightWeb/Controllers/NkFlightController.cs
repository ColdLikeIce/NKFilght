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
    }
}