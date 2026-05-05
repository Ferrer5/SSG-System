using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class TreasurerViewModel
    {
        public int TreasurerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string YearSection { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
