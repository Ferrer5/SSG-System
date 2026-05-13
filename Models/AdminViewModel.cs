using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class AdminViewModel
    {
        public int AdminId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
