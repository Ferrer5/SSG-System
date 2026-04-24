using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MyMvcApp.Models;
using MyMvcApp.Services;
using MyMvcApp.Data;

namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;

    public HomeController(IAuthService authService, IEmailService emailService, ApplicationDbContext context)
    {
        _authService = authService;
        _emailService = emailService;
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public async Task<IActionResult> Dashboard()
    {
        var pendingAccounts = await GetPendingAccountsAsync();
        var students = await GetStudentsAsync();
        
        var dashboardViewModel = new DashboardViewModel
        {
            RequestedAccounts = pendingAccounts,
            Students = students
        };
        
        return View("~/Views/Home/dashboard1.cshtml", dashboardViewModel);
    }

    public IActionResult dashboard1()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Parse role from string
            if (!Enum.TryParse<UserRole>(request.Role, true, out var userRole))
            {
                return Json(new { success = false, message = "Invalid role specified." });
            }

            // Authenticate user
            var result = await _authService.AuthenticateAsync(request.Email, request.Password, userRole);

            if (result.Success)
            {
                // Store user info in session (in production, use proper authentication)
                HttpContext.Session.SetString("UserId", result.Account!.AccountId.ToString());
                HttpContext.Session.SetString("UserRole", result.Account.Role.ToString());
                HttpContext.Session.SetString("Username", result.Account.Username);
                HttpContext.Session.SetString("Email", result.Account.Email);

                return Json(new { 
                    success = true, 
                    message = result.Message,
                    redirectUrl = Url.Action("Dashboard", "Home"),
                    user = new {
                        id = result.Account.AccountId,
                        username = result.Account.Username,
                        email = result.Account.Email,
                        role = result.Account.Role.ToString(),
                        student = result.Student != null ? new {
                            id = result.Student.StudentId,
                            firstname = result.Student.Firstname,
                            lastname = result.Student.Lastname,
                            middlename = result.Student.MiddleName
                        } : null
                    }
                });
            }
            else
            {
                return Json(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Login failed: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        try
        {
            Console.WriteLine("Register method called");
            
            // Log incoming request for debugging
            Console.WriteLine($"Registration request received: Username={request?.Username}, Email={request?.Email}, Role={request?.Role}");
            Console.WriteLine($"Student fields: Firstname={request?.Firstname}, Lastname={request?.Lastname}, CourseCode={request?.CourseCode}, YearLevel={request?.YearLevel}, StudentId={request?.StudentId}");

            // Validate input
            if (request == null)
            {
                return Json(new { success = false, message = "Registration request is null." });
            }

            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Json(new { success = false, message = "Username, email, and password are required." });
            }

            // Validate email format
            if (!IsValidEmail(request.Email))
            {
                return Json(new { success = false, message = "Invalid email format." });
            }

            // Validate password strength
            if (request.Password.Length < 6)
            {
                return Json(new { success = false, message = "Password must be at least 6 characters long." });
            }

            // Role is already parsed as enum from JSON

            // Register account
            var result = await _authService.RegisterAccountAsync(request);

            if (result.Success)
            {
                return Json(new { 
                    success = true, 
                    message = result.Message,
                    account = new {
                        id = result.Account!.AccountId,
                        username = result.Account.Username,
                        email = result.Account.Email,
                        role = result.Account.Role.ToString(),
                        status = result.Account.RequestStatus.ToString()
                    }
                });
            }
            else
            {
                return Json(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Registration failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult TestService()
    {
        try
        {
            // Test basic dependency injection
            if (_authService == null)
            {
                return Json(new { success = false, message = "AuthService is null - DI issue" });
            }

            // Test database connection
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    var canConnect = dbContext.Database.CanConnect();
                    return Json(new { 
                        success = true, 
                        message = $"AuthService working, DB can connect: {canConnect}" 
                    });
                }
                catch (Exception dbEx)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Database connection failed: {dbEx.Message}" 
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Test failed: {ex.Message} - Stack: {ex.StackTrace}" });
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Json(new { success = false, message = "Email is required." });
            }

            // Find account by email
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
            
            if (account == null)
            {
                // Don't reveal that email doesn't exist
                return Json(new { success = true, message = "If an account with that email exists, a password reset link has been sent." });
            }

            // Generate reset token
            var resetToken = Guid.NewGuid().ToString();
            var expirationTime = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

            account.PasswordResetToken = resetToken;
            account.PasswordResetTokenExpires = expirationTime;
            
            await _context.SaveChangesAsync();

            // Send reset email
            var resetUrl = $"{Request.Scheme}://{Request.Host}/Home/ResetPassword?token={resetToken}&email={request.Email}";
            var emailBody = $@"
                <h2>Password Reset Request</h2>
                <p>Hello {account.Username},</p>
                <p>You requested a password reset for your account. Click the link below to reset your password:</p>
                <p><a href='{resetUrl}'>Reset Password</a></p>
                <p>This link will expire in 1 hour.</p>
                <p>If you didn't request this, please ignore this email.</p>
                <br>
                <p>Best regards,<br>SSG System Team</p>";

            await _emailService.SendEmailAsync(account.Email, "Password Reset Request", emailBody);

            return Json(new { success = true, message = "If an account with that email exists, a password reset link has been sent." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to process forgot password: {ex.Message}" });
        }
    }

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        var model = new ResetPasswordViewModel { Token = token, Email = email };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Email))
            {
                return Json(new { success = false, message = "Invalid reset request." });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return Json(new { success = false, message = "New password is required." });
            }

            if (request.NewPassword.Length < 6)
            {
                return Json(new { success = false, message = "Password must be at least 6 characters long." });
            }

            // Find account by email and token
            var account = await _context.Accounts.FirstOrDefaultAsync(a => 
                a.Email == request.Email && 
                a.PasswordResetToken == request.Token && 
                a.PasswordResetTokenExpires > DateTime.UtcNow);

            if (account == null)
            {
                return Json(new { success = false, message = "Invalid or expired reset token." });
            }

            // Update password (plain text as requested)
            account.PasswordHash = request.NewPassword;
            account.PasswordResetToken = null;
            account.PasswordResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Password has been reset successfully. You can now login with your new password." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to reset password: {ex.Message}" });
        }
    }

    private async Task<List<RequestedAccountViewModel>> GetPendingAccountsAsync()
    {
        var pendingAccounts = await _context.Accounts
            .Where(a => a.RequestStatus == RequestStatus.Pending)
            .Include(a => a.Student)
                .ThenInclude(s => s!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Select(a => new RequestedAccountViewModel
            {
                AccountId = a.AccountId,
                StudentId = a.Student != null ? a.Student.StudentId.ToString() : null,
                Fullname = a.Student != null ? 
                    $"{(a.Student.Lastname != null ? a.Student.Lastname.ToUpper() : "")}, {(a.Student.Firstname != null ? a.Student.Firstname.ToUpper() : "")}" : 
                    a.Username.ToUpper(),
                CourseCode = a.Student != null && a.Student.AcademicProfile != null && a.Student.AcademicProfile.Course != null ? a.Student.AcademicProfile.Course.CourseCode : null,
                YearLevel = a.Student != null && a.Student.AcademicProfile != null && a.Student.AcademicProfile.YearLevel != null ? a.Student.AcademicProfile.YearLevel.ToString() : null,
                Section = a.Student != null && a.Student.AcademicProfile != null ? a.Student.AcademicProfile.Section : null,
                CreatedAt = a.CreatedAt,
                Status = a.RequestStatus
            })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return pendingAccounts;
    }

    private async Task<List<StudentViewModel>> GetStudentsAsync()
    {
        var students = await _context.Students
            .Include(s => s.AcademicProfile)
                .ThenInclude(ap => ap!.Course)
            .Include(s => s.Account)
            .Where(s => s.Account != null && s.Account.RequestStatus == RequestStatus.Approve)
            .Select(s => new StudentViewModel
            {
                StudentId = s.StudentId,
                FullName = s.Lastname != null && s.Firstname != null ? $"{s.Lastname.ToUpper()}, {s.Firstname.ToUpper()}" : "N/A",
                CourseCode = s.AcademicProfile != null && s.AcademicProfile.Course != null ? s.AcademicProfile.Course.CourseCode : "N/A",
                YearSection = s.AcademicProfile != null ? $"{s.AcademicProfile.YearLevel ?? "N/A"}-{s.AcademicProfile.Section ?? "N/A"}" : "N/A",
                AccountId = s.AccountId
            })
            .ToListAsync();

        // Sort in memory (client-side) where string.Format works fine
        var sortedStudents = students
            .OrderBy(s => s.FullName)
            .ToList();

        return sortedStudents;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateAccountStatus([FromBody] UpdateAccountStatusRequest request)
    {
        try
        {
            if (request.AccountId <= 0)
            {
                return Json(new { success = false, message = "Invalid account ID." });
            }

            var account = await _context.Accounts.FindAsync(request.AccountId);
            if (account == null)
            {
                return Json(new { success = false, message = "Account not found." });
            }

            // Parse the status
            if (!Enum.TryParse<RequestStatus>(request.Status, true, out var newStatus))
            {
                return Json(new { success = false, message = "Invalid status." });
            }

            account.RequestStatus = newStatus;
            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Account request has been {newStatus.ToString().ToLower()} successfully." 
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to update account status: {ex.Message}" });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UpdateAccountStatusRequest
{
    public int AccountId { get; set; }
    public string Status { get; set; } = string.Empty;
}
