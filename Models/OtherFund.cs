using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class OtherFund
    {
        [Key]
        public int FundId { get; set; }

        [StringLength(200)]
        [Column("source")]
        public string? Source { get; set; }

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public int ReceivedBy { get; set; }

        [Required]
        [Column("received_date")]
        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("ReceivedBy")]
        public Account Receiver { get; set; } = null!;
    }
}
