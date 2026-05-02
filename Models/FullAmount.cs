using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class FullAmount
    {
        [Key]
        public int FullAmountId { get; set; }

        [Required]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("semester")]
        public Semester Semester { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("SchoolYearId")]
        public SchoolYear SchoolYear { get; set; } = null!;
    }

    public enum Semester
    {
        First,
        Second
    }
}
