﻿using Microsoft.AspNetCore.Mvc;
using NkFlightWeb.Impl;
using NkFlightWeb.Service.Dto;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Controllers
{
    public class NkFlightTestController : BaseController
    {
        private readonly INkFlightDomain _domain;

        public NkFlightTestController(INkFlightDomain domain)
        {
            _domain = domain;
        }

        /// <summary>
        ///  爬取城市接口
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpGet("BuildCity")]
        public async Task<string> BuildCity()
        {
            return await _domain.BuildCity();
        }

        /// <summary>
        ///  获取航班信息 存入数据库 默认一个成人 未来一天
        /// </summary>
        /// <param name="activeModel"></param>
        /// <returns></returns>
        [HttpPost("PushAllFlightToDb")]
        public async Task<bool> PushAllFlightToDb([FromBody] SearchDayDto dto)
        {
            return await _domain.PushAllFlightToDb(dto);
        }
    }
}