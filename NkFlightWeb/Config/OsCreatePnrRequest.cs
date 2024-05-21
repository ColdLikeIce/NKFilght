using Qunar.Airtickets.Supplier.Concat.Dtos.Output;

namespace NkFlightWeb.Config
{
    public class pnrCreateModel
    {
        public long orderId { get; set; }
        public string? bookingPNR { get; set; }
    }

    public class OsCreatePnrRequest
    {
        public authration authration { get; set; }
        public long orderId { get; set; }
        public string? bookingPNR { get; set; }
    }

    public class authration
    {
        public string token { get; set; }
        public string appId { get; set; }
        public string timespan { get; set; }
    }

    public class OsCreatePnrResponse : BaseOutputData
    {
        public PnrreturnModel result { get; set; }
    }

    public class PnrreturnModel
    {
        public string bookingPNR { get; set; }
    }
}