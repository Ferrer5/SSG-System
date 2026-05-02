using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class SchoolYear
    {
        [Key]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("year_start")]
        public int YearStart { get; set; }

        [Required]
        [Column("year_end")]
        public int YearEnd { get; set; }

        // Navigation properties
        public virtual ICollection<FullAmount> FullAmounts { get; set; } = new List<FullAmount>();
        public virtual ICollection<OrgFeePayment> OrgFeePayments { get; set; } = new List<OrgFeePayment>();
    }
}
