using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NkFlightWeb.Entity
{
    [Table("BasParameter")]
    public class BasParameter
    {
        [Key]
        public int Id { get; set; }

        public string key { get; set; }
        public string value { get; set; }
        public string cookies { get; set; }
        public DateTime? expireTime { get; set; }
        public DateTime? ctime { get; set; }
    }
}