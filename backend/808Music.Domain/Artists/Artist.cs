using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace _808Music.Domain.Artists
{
    public class Artist
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string ProfilePhotoPath { get; set; } = string.Empty;
        public string ProfileBackgroundPath { get; set; } = string.Empty;
        public int Followers { get; set; }
        public bool IsFlaggedForDeletion { get; set; }
        public DateTime DeletionDate { get; set; }
    }
}
