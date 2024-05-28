using NkFlightWeb.Entity;

namespace NkFlightWeb.Service.Dto
{
    public class JourneyInsertModel
    {
        public int index { get; set; }
        public NKJourney NKJourney { get; set; }
        public List<NKSegment> SegList { get; set; }
        public List<NKSegment> dbRegList { get; set; }
    }
}