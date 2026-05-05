using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    [Table("full_amount")]
    public class FullAmount
    {
        [Key]
        [Column("full_amount_id")]
        public int FullAmountId { get; set; }

        [Required]
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("semester")]
        public Semester Semester { get; set; }

        [Required]
        [Column("full_amount")]
        public decimal Amount { get; set; }

        
        [ForeignKey("SchoolYearId")]
        public SchoolYear SchoolYear { get; set; } = null!;
    }

    public enum Semester
    {
        First,
        Second
    }
}
