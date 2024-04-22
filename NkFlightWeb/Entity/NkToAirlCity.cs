using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NkFlightWeb.Entity
{
    [Table("NkToAirlCity")]
    public class NkToAirlCity
    {
        [Key]
        public int Id { get; set; }

        public string city { get; set; }
        public string searchcity { get; set; }
    }
}