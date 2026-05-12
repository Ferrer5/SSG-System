namespace MyMvcApp.Models
{
    public class StudentDashboardViewModel
    {
        public List<StudentPaymentHistoryViewModel> PaymentHistory { get; set; } = new();
    }

    public class StudentPaymentHistoryViewModel
    {
        public string Term { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public string OverallStatus { get; set; } = string.Empty;
        public List<StudentPaymentViewModel> Payments { get; set; } = new();
    }

    public class StudentPaymentViewModel
    {
        public string Semester { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
