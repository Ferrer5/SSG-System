using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    [Table("courses")]
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required]
        [StringLength(20)]
        [Column("course_code")]
        public string CourseCode { get; set; } = string.Empty;

        [StringLength(100)]
        [Column("course_name")]
        public string? CourseName { get; set; }

        // Navigation properties
        public virtual ICollection<AcademicProfile> AcademicProfiles { get; set; } = new List<AcademicProfile>();
    }
}
