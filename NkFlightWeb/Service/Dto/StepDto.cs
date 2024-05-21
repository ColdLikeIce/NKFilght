using DtaAccess.Domain.Enums;
using Qunar.Airtickets.Supplier.Concat.Dtos.Models;

namespace NkFlightWeb.Service.Dto
{
    public class StepDto
    {
        public string carrier { get; set; }
        public string startArea { get; set; }
        public string endArea { get; set; }
        public DateTime? fromTime { get; set; }
        public int? childSourceNum { get; set; }
        public int? adtSourceNum { get; set; }
        public CabinClass? cabinClass { get; set; }
        public string Cabin { get; set; }

        //
        // 摘要:
        //     航班号
        public string FlightNumber { get; set; }

        public bool IsBack { get; set; }

        //
        // 摘要:
        //     去程
        public List<SegmentLineDetail> FromSegments { get; set; } = new List<SegmentLineDetail>();

        //
        // 摘要:
        //     返程
        public List<SegmentLineDetail> RetSegments { get; set; } = new List<SegmentLineDetail>();
    }

    public class SegmentLineDetail
    {
        public string DepDate { get; set; }

        //
        // 摘要:
        //     出发机场
        public string DepAirport { get; set; }

        //
        // 摘要:
        //     到达机场
        public string ArrAirport { get; set; }
    }
}