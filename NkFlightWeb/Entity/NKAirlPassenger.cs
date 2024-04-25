using DtaAccess.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace NkFlightWeb.Entity
{
    [Table("NK_AirlPassenger")]
    public class NKAirlPassenger
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
        public string? SupplierOrderId { get; set; }
        public string? AirlinePnrCode { get; set; }
        public string? PNRCode { get; set; }
        public GenderType Gender { get; set; }
        public PassengerType PassengerType { get; set; }
        public string? Birthday { get; set; }
        public string? Nationality { get; set; }
        public CardType CredentialsType { get; set; }
        public string? CredentialsNum { get; set; }
        public string? CredentialsExpired { get; set; }
        public string? CredentialIssuingCountry { get; set; }
        public DateTime? ctime { get; set; }
        public virtual NKFlightOrder AirlOrder { get; set; }
    }
}