using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class DashboardViewModel
    {
        public List<RequestedAccountViewModel> RequestedAccounts { get; set; } = new();
        
        public List<StudentViewModel> Students { get; set; } = new();
    }
}
