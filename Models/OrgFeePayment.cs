using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    [Table("org_fee_payments")]
    public class OrgFeePayment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("semester")]
        public Semester Semester { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column("amount_required", TypeName = "decimal(10,2)")]
        public decimal AmountRequired { get; set; }

        [Required]
        [Column("payment_status")]
        public PaymentStatus PaymentStatus { get; set; }

        [Required]
        public int ReceivedBy { get; set; }

        [Required]
        [Column("payment_date")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [ForeignKey("SchoolYearId")]
        public SchoolYear SchoolYear { get; set; } = null!;

        [ForeignKey("ReceivedBy")]
        public Account Receiver { get; set; } = null!;

        public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }

    public enum PaymentStatus
    {
        Partial,
        Paid
    }
}
