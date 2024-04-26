namespace NkFlightWeb.Service.Dto
{
    public class TokenUserModel
    {
        public DateTime? PassTime { get; set; }
        public DateTime? UseTime { get; set; }
        public string? Headers { get; set; }
        public string? Cookies { get; set; }
        public string? Token { get; set; }
    }
}