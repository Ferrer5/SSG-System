using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class VerifyCodeRequest
    {
        [Required]
        public required string StudentId { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public required string Code { get; set; }
    }
}
