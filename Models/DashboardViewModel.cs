using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class DashboardViewModel
    {
        public List<RequestedAccountViewModel> RequestedAccounts { get; set; } = new();
        
        public List<RequestedAccountViewModel> AllAccountRequests { get; set; } = new();
        
        public List<StudentViewModel> Students { get; set; } = new();
        
        public List<AdminViewModel> Admins { get; set; } = new();
        
        public List<TreasurerViewModel> Treasurers { get; set; } = new();
        
        public List<ProfessorViewModel> Professors { get; set; } = new();
        
        public int ApprovedAccountsCount { get; set; }
    }

    public class AdminViewModel
    {
        public int AdminId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

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

    public class ProfessorViewModel
    {
        public int ProfessorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
