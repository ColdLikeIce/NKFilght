using DtaAccess.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace NkFlightWeb.Entity
{
    [Table("NK_FlightOrder")]
    public class NKFlightOrder
    {
        [Key]
        public string OrderId { get; set; }

        public long platOrderId { get; set; }
        public string PNR { get; set; }
        public string PayCode { get; set; }
        public string? Refer { get; set; }
        public CabinClass? CabinClass { get; set; }
        public CurrencyEnums Currency { get; set; }
        public decimal? SumPrice { get; set; }
        public decimal? TaxPrice { get; set; }
        public decimal? OtherPrice { get; set; }

        /// <summary>
        /// 订票时间
        /// </summary>
        public DateTime? BookDate { get; set; }

        /// <summary>
        /// 截止支付时间
        /// </summary>
        public DateTime? FinishPayDate { get; set; }

        public OrderStatus? Status { get; set; }

        public string? RateCode { get; set; }
        public string? startArea { get; set; }
        public string? endArea { get; set; }
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public string? Carrier { get; set; }
        public string? ConcatName { get; set; }
        public string? ConcatPhone { get; set; }
        public string? ConcatEmail { get; set; }
        public DateTime? FlyDate { get; set; }
        public DateTime? CTime { get; set; }
        public DateTime? UTime { get; set; }
        public DateTime? CancelTime { get; set; }
        public virtual List<NKAirlSegment> Segment { get; set; }
        public virtual List<NKAirlPassenger> NKAirlPassenger { get; set; }
    }

    public enum OrderStatus
    {
        //
        // 摘要:
        //     待支付
        [Description("待支付")]
        Pending = 1,

        //
        // 摘要:
        //     出票中
        [Description("出票中")]
        Ticketing,

        //
        // 摘要:
        //     已出票
        [Description("出票完成")]
        TicketIssued,

        //
        // 摘要:
        //     出票失败
        [Description("出票失败")]
        TicketIssuanceFailed,

        //
        // 摘要:
        //     取消
        [Description("取消")]
        Canceled
    }
}