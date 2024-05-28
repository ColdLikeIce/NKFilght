using Qunar.Airtickets.Supplier.Concat.Dtos.Models;

namespace NkFlightWeb.Service.Dto
{
    public class PriceDetailDict
    {
        public List<SearchAirticket_PriceDetail> Detail { get; set; }
        public string Key { get; set; }
    }
}