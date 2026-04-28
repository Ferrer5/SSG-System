using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class VerifyCodeRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public required string Code { get; set; }
    }
}
