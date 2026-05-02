using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class Receipt
    {
        [Key]
        public int ReceiptId { get; set; }

        [Required]
        [StringLength(50)]
        [Column("receipt_number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        public int? PaymentId { get; set; }

        [Required]
        public int IssuedBy { get; set; }

        // Navigation properties
        [ForeignKey("PaymentId")]
        public OrgFeePayment? Payment { get; set; }

        [ForeignKey("IssuedBy")]
        public Account Issuer { get; set; } = null!;
    }
}
