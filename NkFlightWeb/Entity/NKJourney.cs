using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NkFlightWeb.Entity
{
    [Table("NK_Journey")]
    public class NKJourney
    {
        [Key]
        public Guid JourneyId { get; set; }

        public int TripType { get; set; } //行程类型：单程 1  往返  2
        public string? DepCity { get; set; }
        public string? ArrCity { get; set; }
        public DateTime? DepTime { get; set; }
        public DateTime? ArrTime { get; set; }
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public DateTime RequestTime { get; set; }
    }
}