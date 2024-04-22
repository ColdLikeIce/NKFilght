namespace NkFlightWeb.Service.Dto
{
    public class SourceQueryDto
    {
        public bool includeWifiAvailability { get; set; } = true;
        public List<criteria> criteria { get; set; }
        public passengers passengers { get; set; }
        public codes codes { get; set; } = new codes();
        public fareFilters fareFilters { get; set; } = new fareFilters();
        public string taxesAndFees { get; set; } = "TaxesAndFees";
        public List<object> originalJourneyKeys { get; set; } = new List<object>();
        public object originalBookingRecordLocator { get; set; }
        public int infantCount { get; set; } = 0;
        public List<string> birthDates = new List<string>();
    }

    public class criteria
    {
        public stations stations { get; set; }
        public dates dates { get; set; }
        public filters filters { get; set; } = new filters();
    }

    public class filters
    {
        public string filter { get; set; } = "Default";
    }

    public class dates
    {
        public string beginDate { get; set; }
        public string endDate { get; set; }
    }

    public class stations
    {
        public List<string> originStationCodes { get; set; }
        public List<string> destinationStationCodes { get; set; }
        public bool searchDestinationMacs { get; set; } = false;
        public bool searchOriginMacs { get; set; } = false;
    }

    public class passengers
    {
        public List<passengersType> types { get; set; }
    }

    public class passengersType
    {
        public string type { get; set; }
        public int count { get; set; }
    }

    public class codes
    {
        public string currency { get; set; } = "USD";
    }

    public class fareFilters
    {
        public string loyalty { get; set; } = "MonetaryOnly";
        public List<string> types = new List<string>();
        public int classControl = 1;
    }
}