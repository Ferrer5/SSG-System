using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class Student
    {
        public int StudentId { get; set; }

        [Required]
        public int AccountId { get; set; }

        [StringLength(50)]
        public string? Lastname { get; set; }

        [StringLength(50)]
        public string? Firstname { get; set; }

        [StringLength(10)]
        public string? MiddleName { get; set; }

        // Navigation properties
        [ForeignKey("AccountId")]
        public Account Account { get; set; } = null!;
        
        public AcademicProfile? AcademicProfile { get; set; }
    }
}
