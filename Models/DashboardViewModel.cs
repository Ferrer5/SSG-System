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
}
