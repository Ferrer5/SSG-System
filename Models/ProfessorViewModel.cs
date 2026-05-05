using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class ProfessorViewModel
    {
        public int ProfessorId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
