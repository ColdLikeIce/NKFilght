using DtaAccess.Domain.Enums.Qunar.Airtickets.Supplier.Concat.Dtos.Enums;
using DtaAccess.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NkFlightWeb.Entity
{
    [Table("NK_Segment")]
    public class NKSegment
    {
        [Key]
        public Guid? SegmentId { get; set; }

        public Guid? JourneyId { get; set; }
        public string? RateCode { get; set; }
        public string? Carrier { get; set; }
        public string? Cabin { get; set; }
        public int? CabinClass { get; set; }
        public string? FlightNumber { get; set; }
        public string? DepAirport { get; set; }
        public string? ArrAirport { get; set; }
        public string? DepDate { get; set; }
        public string? ArrDate { get; set; }
        public string? StopCities { get; set; }
        public bool? CodeShare { get; set; }
        public string? ShareCarrier { get; set; }
        public string? ShareFlightNumber { get; set; }
        public string? AircraftCode { get; set; }
        public int? Group { get; set; }
        public string? FareBasis { get; set; }
        public int? GdsType { get; set; }
        public string? PosArea { get; set; }
        public string? BaggageRule { get; set; }
        public string? AirlinePnrCode { get; set; }
    }
}