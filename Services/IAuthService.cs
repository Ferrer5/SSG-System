using MyMvcApp.Models;

namespace MyMvcApp.Services
{
    public interface IAuthService
    {
        Task<AuthResult> AuthenticateAsync(string email, string password, UserRole role);
        Task<AuthResult> AuthenticateByUsernameAsync(string username, string password, UserRole role);
        Task<RegistrationResult> RegisterAccountAsync(RegistrationRequest request);
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Account? Account { get; set; }
        public Student? Student { get; set; }
    }

    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Account? Account { get; set; }
        public Student? Student { get; set; }
    }

    public class RegistrationRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        
        // Student specific fields
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? MiddleName { get; set; }
        public string? CourseCode { get; set; }
        public YearLevel? YearLevel { get; set; }
        public string? Section { get; set; }
        public string? StudentId { get; set; }
    }
}
