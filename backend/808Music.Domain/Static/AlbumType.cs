using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace _808Music.Domain.Static
{
    public class AlbumType
    {
        [Key]
        public int Id { get; set; }
        public string Type { get; set; }
    }
}
