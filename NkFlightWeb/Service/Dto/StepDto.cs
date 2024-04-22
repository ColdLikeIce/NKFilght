using DtaAccess.Domain.Enums;

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
    }
}