using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    [Table("academic_profile")]
    public class AcademicProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AcademicProfileId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Column("year_level")]
        public string? YearLevel { get; set; }

        [StringLength(50)]
        public string? Section { get; set; }

        [Required]
        [Column("academic_status")]
        public AcademicStatus AcademicStatus { get; set; } = AcademicStatus.Enrolled;

        // Navigation properties
        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [ForeignKey("CourseId")]
        public Course Course { get; set; } = null!;
    }

    public enum YearLevel
    {
        First = 1,    // Maps to database '1'
        Second = 2,   // Maps to database '2'
        Third = 3,     // Maps to database '3'
        Fourth = 4     // Maps to database '4'
    }

    public enum AcademicStatus
    {
        Enrolled,
        Transferred,
        Graduated,
        Dropped
    }
}
