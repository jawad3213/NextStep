using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Modules.Profile.Models
{
    [Table("skill_keyword")]
    public class Keyword
    {
        [Key]
        [Column("id_skill_keyword")]
        public Guid Id { get; set; }

        [Column("mot")]
        public string Mot { get; set; } = string.Empty;

        [Column("categorie")]
        public string Categorie { get; set; } = "Technique";
    }
}
