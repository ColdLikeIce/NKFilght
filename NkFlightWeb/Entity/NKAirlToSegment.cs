using DtaAccess.Domain.Enums.Qunar.Airtickets.Supplier.Concat.Dtos.Enums;
using DtaAccess.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace NkFlightWeb.Entity
{
    [Table("NK_AirlToSegment")]
    public class NKAirlToSegment
    {
        [Key]
        public int? Id { get; set; }

        public string? OrderId { get; set; }
        public string? RateCode { get; set; }
        public string? Carrier { get; set; }
        public string? Cabin { get; set; }
        public CabinClass CabinClass { get; set; }
        public string? FlightNumber { get; set; }
        public string? DepAirport { get; set; }
        public string? ArrAirport { get; set; }
        public string? DepDate { get; set; }
        public string? ArrDate { get; set; }
        public string? StopCities { get; set; }
        public bool CodeShare { get; set; }
        public string? ShareCarrier { get; set; }
        public string? ShareFlightNumber { get; set; }
        public string? AircraftCode { get; set; }
        public int? Group { get; set; }
        public string? FareBasis { get; set; }
        public GdsType? GdsType { get; set; }
        public string? PosArea { get; set; }
        public string? BaggageRule { get; set; }
        public string? AirlinePnrCode { get; set; }
        public virtual NKFlightOrder AirlOrder { get; set; }
    }
}