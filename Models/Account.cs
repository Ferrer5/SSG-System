using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class Account
    {
        [Key]
        public int AccountId { get; set; }

        [Required]
        [StringLength(150)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("roles")]
        public UserRole Role { get; set; }

        [Required]
        [Column("request_status")]
        public RequestStatus RequestStatus { get; set; } = RequestStatus.Pending;

        [Required]
        [Column("is_online")]
        public bool IsOnline { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        [Column("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        [Column("password_reset_token_expires")]
        public DateTime? PasswordResetTokenExpires { get; set; }

        // Navigation properties
        public Student? Student { get; set; }
    }

    public enum UserRole
    {
        Student,
        Treasurer,
        Admin
    }

    public enum RequestStatus
    {
        Pending,
        Approve,
        Rejected
    }
}
