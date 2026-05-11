using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class PaymentHistoryViewModel
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public string Term { get; set; } = string.Empty;

        [Required]
        public string Course { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Semester { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;

        [Required]
        public string OverallStatus { get; set; } = string.Empty;

        public List<PaymentDetailViewModel> Payments { get; set; } = new();
    }

    public class PaymentDetailViewModel
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public string Semester { get; set; } = string.Empty;

        [Required]
        public string Course { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
