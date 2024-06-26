﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NkFlightWeb.Entity
{
    [Table("Nk_ToAirlCity")]
    public class NkToAirlCity
    {
        [Key]
        public int Id { get; set; }

        public string city { get; set; }
        public string searchcity { get; set; }
        public string fromcity { get; set; }
        public string searchFromCity { get; set; }
    }
}