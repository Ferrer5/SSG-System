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

    // ----------------------------------------------------------------
    // PUBLIC PAGES
    // ----------------------------------------------------------------

    public IActionResult Index()
    {
        // Redirect already-logged-in users away from the login page
        if (HttpContext.Session.GetString("UserId") != null)
        {
            return RedirectToDashboard(HttpContext.Session.GetString("UserRole"));
        }
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Contacts()
    {
        return View();
    }

    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("UserId") != null)
        {
            return RedirectToDashboard(HttpContext.Session.GetString("UserRole"));
        }
        // Login UI lives on Index (overlay); there is no separate Login.cshtml view.
        return RedirectToAction(nameof(Index), new { login = 1 });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // ----------------------------------------------------------------
    // DASHBOARDS — role-guarded
    // ----------------------------------------------------------------

    public async Task<IActionResult> Dashboard()
    {
        // Guard: only allow logged-in non-admin users
        var role = HttpContext.Session.GetString("UserRole");
        if (string.IsNullOrEmpty(role))
            return RedirectToAction("Login", "Home");

        if (role == "Admin")
            return RedirectToAction("AdminDashboard", "Home");

        // If this user has a dedicated role dashboard action, route there.
        if (role == "Treasurer")
            return RedirectToAction("TreasurerDashboard", "Home");

        if (role == "Professor")
            return RedirectToAction("ProfessorDashboard", "Home");

        var pendingAccounts = await GetPendingAccountsAsync();
        var students = await GetStudentsAsync();

        var model = new DashboardViewModel
        {
            RequestedAccounts = pendingAccounts,
            Students = students
        };

        return View("~/Views/Dashboard/student_dashboard.cshtml", model);
    }

    public async Task<IActionResult> AdminDashboard()
    {
        // Guard: only Admins can access this page
        var role = HttpContext.Session.GetString("UserRole");
        if (string.IsNullOrEmpty(role))
            return RedirectToAction("Login", "Home");

        if (role != "Admin")
            return RedirectToAction("Dashboard", "Home");

        var pendingAccounts = await GetPendingAccountsAsync();
        var allAccountRequests = await GetAllAccountRequestsAsync();
        var students = await GetStudentsAsync();
        var admins = await GetAdminsAsync();
        var treasurers = await GetTreasurersAsync();
        var professors = await GetProfessorsAsync();
        var approvedAccountsCount = await GetApprovedAccountsCountAsync();

        var model = new DashboardViewModel
        {
            RequestedAccounts = pendingAccounts,
            AllAccountRequests = allAccountRequests,
            Students = students,
            Admins = admins,
            Treasurers = treasurers,
            Professors = professors,
            ApprovedAccountsCount = approvedAccountsCount
        };

        return View("~/Views/Dashboard/admin_dashboard.cshtml", model);
    }

    public IActionResult TreasurerDashboard()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (string.IsNullOrEmpty(role))
            return RedirectToAction("Login", "Home");

        if (role != "Treasurer")
            return RedirectToDashboard(role);

        return View("~/Views/Dashboard/treasurer_dashboard.cshtml");
    }

    public IActionResult ProfessorDashboard()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (string.IsNullOrEmpty(role))
            return RedirectToAction("Login", "Home");

        if (role != "Professor")
            return RedirectToDashboard(role);

        return View("~/Views/Dashboard/professor_dashboard.cshtml");
    }

    // ----------------------------------------------------------------
    // LOGIN
    // ----------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SchoolId) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Json(new { success = false, message = "School ID and password are required." });
            }

            // Step 1: Find account by School ID
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.SchoolId.ToLower() == request.SchoolId.ToLower());

            if (account == null)
            {
                return Json(new { success = false, message = "Invalid School ID or password." });
            }

            // Step 2: Check approval and active status
            if (account.RequestStatus != RequestStatus.Approved)
            {
                return Json(new { success = false, message = "Your account is not yet approved. Please wait for admin approval." });
            }

            if (!account.IsActive)
            {
                return Json(new { success = false, message = "Your account has been deactivated. Please contact the administrator." });
            }

            // Step 3: Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            {
                return Json(new { success = false, message = "Invalid School ID or password." });
            }

            // Step 4: Load the user profile
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == account.AccountId);

            // Step 5: Store session
            HttpContext.Session.SetString("UserId",    account.AccountId.ToString());
            HttpContext.Session.SetString("UserRole",  account.Role.ToString());
            HttpContext.Session.SetString("SchoolId",  account.SchoolId);
            HttpContext.Session.SetString("Email",     account.Email ?? "");
            HttpContext.Session.SetString("FirstName", user?.FirstName ?? "");
            HttpContext.Session.SetString("LastName",  user?.LastName ?? "");

            // Step 6: Determine redirect by role
            string redirectUrl = account.Role switch
            {
                UserRole.Admin => Url.Action("AdminDashboard", "Home")!,
                UserRole.Treasurer => Url.Action("TreasurerDashboard", "Home")!,
                UserRole.Professor => Url.Action("ProfessorDashboard", "Home")!,
                _ => Url.Action("Dashboard", "Home")!
            };

            return Json(new
            {
                success = true,
                message = "Login successful.",
                redirectUrl,
                user = new
                {
                    id         = account.AccountId,
                    schoolId   = account.SchoolId,
                    email      = account.Email,
                    role       = account.Role.ToString(),
                    firstName  = user?.FirstName,
                    lastName   = user?.LastName,
                    middleName = user?.MiddleName
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Login failed: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // LOGOUT
    // ----------------------------------------------------------------

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Home");
    }

    // ----------------------------------------------------------------
    // REGISTER
    // ----------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        try
        {
            if (request == null)
                return Json(new { success = false, message = "Registration request is null." });

            if (string.IsNullOrWhiteSpace(request.SchoolId) || string.IsNullOrWhiteSpace(request.Password))
                return Json(new { success = false, message = "School ID and password are required." });

            if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
                return Json(new { success = false, message = "Invalid email format." });

            if (request.Password.Length < 6)
                return Json(new { success = false, message = "Password must be at least 6 characters long." });

            var result = await _authService.RegisterAccountAsync(request);

            if (result.Success)
            {
                return Json(new
                {
                    success = true,
                    message = result.Message,
                    account = new
                    {
                        id       = result.Account!.AccountId,
                        schoolId = result.Account.SchoolId,
                        email    = result.Account.Email,
                        role     = result.Account.Role.ToString(),
                        status   = result.Account.RequestStatus.ToString()
                    }
                });
            }

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Registration failed: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // FORGOT PASSWORD
    // ----------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.StudentId))
                return Json(new { success = false, message = "Student ID is required." });

            var account = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.SchoolId.ToLower() == request.StudentId.ToLower()
                                       && a.Role == UserRole.Student);

            // Always return the same message — don't reveal if ID exists or not
            var genericMessage = $"If that ID exists, a verification code has been sent to the registered email.";

            if (account == null || account.User == null)
                return Json(new { success = true, message = genericMessage });

            // Generate 6-digit code
            var verificationCode    = new Random().Next(100000, 999999).ToString();
            var expirationTime      = DateTime.UtcNow.AddMinutes(15);

            account.PasswordResetToken        = verificationCode;
            account.PasswordResetTokenExpires = expirationTime;
            await _context.SaveChangesAsync();

            var emailBody = $@"Hello {account.User.FirstName ?? account.SchoolId},<br><br>
Your password reset verification code is:<br><br>
<strong>{verificationCode}</strong><br><br>
This code expires in 15 minutes.<br><br>
If you did not request this, please ignore this email.<br><br>
Best regards,<br>SSG Financial Management System";

            await _emailService.SendEmailAsync(account.Email ?? "", "Password Reset Code", emailBody);

            return Json(new { success = true, message = genericMessage, studentId = request.StudentId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to process request: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // VERIFY RESET CODE
    // ----------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyCodeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.StudentId) || string.IsNullOrWhiteSpace(request.Code))
                return Json(new { success = false, message = "Student ID and verification code are required." });

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.SchoolId.ToLower() == request.StudentId.ToLower()
                                       && a.Role == UserRole.Student);

            if (account == null
                || account.PasswordResetToken != request.Code
                || account.PasswordResetTokenExpires == null
                || DateTime.UtcNow > account.PasswordResetTokenExpires)
            {
                return Json(new { success = false, message = "Invalid or expired verification code." });
            }

            // Mark as verified
            account.PasswordResetToken        = "verified";
            account.PasswordResetTokenExpires = DateTime.UtcNow.AddMinutes(30);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Code verified. You can now reset your password." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to verify code: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // RESET PASSWORD
    // ----------------------------------------------------------------

    [HttpGet]
    public IActionResult ResetPassword(string token, string studentId)
    {
        var model = new ResetPasswordViewModel { Token = token, StudentId = studentId };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.StudentId))
                return Json(new { success = false, message = "Student ID is required." });

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return Json(new { success = false, message = "Password must be at least 6 characters long." });

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.SchoolId.ToLower() == request.StudentId.ToLower()
                                       && a.Role == UserRole.Student);

            if (account == null
                || account.PasswordResetToken != "verified"
                || account.PasswordResetTokenExpires == null
                || account.PasswordResetTokenExpires <= DateTime.UtcNow)
            {
                return Json(new { success = false, message = "Invalid or expired reset token." });
            }

            account.PasswordHash              = AuthService.HashPassword(request.NewPassword);
            account.PasswordResetToken        = null;
            account.PasswordResetTokenExpires = null;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Password reset successfully. You can now log in." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to reset password: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // ACCOUNT MANAGEMENT
    // ----------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> UpdateAccountStatus([FromBody] UpdateAccountStatusRequest request)
    {
        try
        {
            // Guard: only Admin can approve/reject accounts
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            if (request.AccountId <= 0)
                return Json(new { success = false, message = "Invalid account ID." });

            var account = await _context.Accounts.FindAsync(request.AccountId);
            if (account == null)
                return Json(new { success = false, message = "Account not found." });

            if (!Enum.TryParse<RequestStatus>(request.Status, true, out var newStatus))
                return Json(new { success = false, message = "Invalid status." });

            account.RequestStatus = newStatus;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Account has been {newStatus.ToString().ToLower()} successfully."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to update account status: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // COURSES
    // ----------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        try
        {
            var courses = await _context.Courses
                .OrderBy(c => c.CourseCode)
                .Select(c => new { c.CourseId, c.CourseCode })
                .ToListAsync();

            return Json(new { success = true, courses });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to get courses: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // DEBUG / TEST (remove before production)
    // ----------------------------------------------------------------

    [HttpGet]
    public IActionResult TestService()
    {
        try
        {
            if (_authService == null)
                return Json(new { success = false, message = "AuthService is null — DI issue." });

            var canConnect = _context.Database.CanConnect();
            return Json(new { success = true, message = $"DB can connect: {canConnect}" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Test failed: {ex.Message}" });
        }
    }

    // ----------------------------------------------------------------
    // ERROR
    // ----------------------------------------------------------------

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // ----------------------------------------------------------------
    // PRIVATE HELPERS
    // ----------------------------------------------------------------

    private IActionResult RedirectToDashboard(string? role)
    {
        return role switch
        {
            "Admin" => RedirectToAction("AdminDashboard", "Home"),
            "Treasurer" => RedirectToAction("TreasurerDashboard", "Home"),
            "Professor" => RedirectToAction("ProfessorDashboard", "Home"),
            _ => RedirectToAction("Dashboard", "Home")
        };
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

    private async Task<List<RequestedAccountViewModel>> GetPendingAccountsAsync()
    {
        return await _context.Accounts
            .Where(a => a.RequestStatus == RequestStatus.Pending)
            .Include(a => a.User)
                .ThenInclude(u => u!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Select(a => new RequestedAccountViewModel
            {
                AccountId  = a.AccountId,
                StudentId  = a.User != null ? a.User.UserId.ToString() : null,
                Fullname   = a.User != null
                    ? $"{(a.User.LastName != null ? a.User.LastName.ToUpper() : "")}, {(a.User.FirstName != null ? a.User.FirstName.ToUpper() : "")}"
                    : a.SchoolId.ToUpper(),
                CourseCode = a.User != null && a.User.AcademicProfile != null && a.User.AcademicProfile.Course != null
                    ? a.User.AcademicProfile.Course.CourseCode : null,
                YearLevel  = a.User != null && a.User.AcademicProfile != null && a.User.AcademicProfile.YearLevel != null
                    ? a.User.AcademicProfile.YearLevel.ToString() : null,
                Section    = a.User != null && a.User.AcademicProfile != null
                    ? a.User.AcademicProfile.Section : null,
                CreatedAt  = a.CreatedAt,
                Status     = a.RequestStatus
            })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    private async Task<List<RequestedAccountViewModel>> GetAllAccountRequestsAsync()
    {
        return await _context.Accounts
            .Include(a => a.User)
                .ThenInclude(u => u!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Select(a => new RequestedAccountViewModel
            {
                AccountId  = a.AccountId,
                StudentId  = a.User != null ? a.User.UserId.ToString() : null,
                Fullname   = a.User != null
                    ? $"{(a.User.LastName != null ? a.User.LastName.ToUpper() : "")}, {(a.User.FirstName != null ? a.User.FirstName.ToUpper() : "")}"
                    : a.SchoolId.ToUpper(),
                CourseCode = a.User != null && a.User.AcademicProfile != null && a.User.AcademicProfile.Course != null
                    ? a.User.AcademicProfile.Course.CourseCode : null,
                YearLevel  = a.User != null && a.User.AcademicProfile != null && a.User.AcademicProfile.YearLevel != null
                    ? a.User.AcademicProfile.YearLevel.ToString() : null,
                Section    = a.User != null && a.User.AcademicProfile != null
                    ? a.User.AcademicProfile.Section : null,
                CreatedAt  = a.CreatedAt,
                Status     = a.RequestStatus
            })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    private async Task<List<StudentViewModel>> GetStudentsAsync()
    {
        var students = await _context.Users
            .Include(u => u.AcademicProfile)
                .ThenInclude(ap => ap!.Course)
            .Include(u => u.Account)
            .Where(u => u.Account != null && u.Account.RequestStatus == RequestStatus.Approved)
            .Select(u => new StudentViewModel
            {
                StudentId   = u.UserId,
                FullName    = u.LastName != null && u.FirstName != null
                    ? $"{u.LastName.ToUpper()}, {u.FirstName.ToUpper()}" : "N/A",
                CourseCode  = u.AcademicProfile != null && u.AcademicProfile.Course != null
                    ? u.AcademicProfile.Course.CourseCode : "N/A",
                YearSection = u.AcademicProfile != null
                    ? $"{(u.AcademicProfile.YearLevel.HasValue ? u.AcademicProfile.YearLevel.Value.ToString() : "N/A")}-{(u.AcademicProfile.Section ?? "N/A")}"
                    : "N/A",
                AccountId   = u.AccountId
            })
            .ToListAsync();

        return students.OrderBy(s => s.FullName).ToList();
    }

    private async Task<List<AdminViewModel>> GetAdminsAsync()
    {
        var admins = await _context.Accounts
            .Include(a => a.User)
            .Where(a => a.Role == UserRole.Admin && a.RequestStatus == RequestStatus.Approved)
            .Select(a => new AdminViewModel
            {
                AdminId = a.AccountId,
                FullName = a.User != null && a.User.LastName != null && a.User.FirstName != null
                    ? $"{a.User.LastName.ToUpper()}, {a.User.FirstName.ToUpper()}" : "N/A",
                Email = a.Email ?? "N/A",
                SchoolId = a.SchoolId ?? "N/A",
                Role = a.Role.ToString()
            })
            .ToListAsync();

        return admins.OrderBy(a => a.FullName).ToList();
    }

    private async Task<List<TreasurerViewModel>> GetTreasurersAsync()
    {
        var treasurers = await _context.Accounts
            .Include(a => a.User)
                .ThenInclude(u => u!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Where(a => a.Role == UserRole.Treasurer && a.RequestStatus == RequestStatus.Approved)
            .Select(a => new TreasurerViewModel
            {
                TreasurerId = a.AccountId,
                FullName = a.User != null && a.User.LastName != null && a.User.FirstName != null
                    ? $"{a.User.LastName.ToUpper()}, {a.User.FirstName.ToUpper()}" : "N/A",
                Email = a.Email ?? "N/A",
                SchoolId = a.SchoolId ?? "N/A",
                CourseCode = a.User != null && a.User.AcademicProfile != null && a.User.AcademicProfile.Course != null
                    ? a.User.AcademicProfile.Course.CourseCode : "N/A",
                YearSection = a.User != null && a.User.AcademicProfile != null
                    ? $"{(a.User.AcademicProfile.YearLevel.HasValue ? a.User.AcademicProfile.YearLevel.Value.ToString() : "N/A")}-{(a.User.AcademicProfile.Section ?? "N/A")}"
                    : "N/A",
                Role = a.Role.ToString()
            })
            .ToListAsync();

        return treasurers.OrderBy(t => t.FullName).ToList();
    }

    private async Task<List<ProfessorViewModel>> GetProfessorsAsync()
    {
        var professors = await _context.Accounts
            .Include(a => a.User)
            .Where(a => a.Role == UserRole.Professor && a.RequestStatus == RequestStatus.Approved)
            .Select(a => new ProfessorViewModel
            {
                ProfessorId = a.AccountId,
                FullName = a.User != null && a.User.LastName != null && a.User.FirstName != null
                    ? $"{a.User.LastName.ToUpper()}, {a.User.FirstName.ToUpper()}" : "N/A",
                Email = a.Email ?? "N/A",
                SchoolId = a.SchoolId ?? "N/A",
                Department = "N/A", // TODO: Add department field to User or Account model
                Role = a.Role.ToString()
            })
            .ToListAsync();

        return professors.OrderBy(p => p.FullName).ToList();
    }

    private async Task<int> GetApprovedAccountsCountAsync()
    {
        return await _context.Accounts
            .Where(a => a.RequestStatus == RequestStatus.Approved)
            .CountAsync();
    }
}

// ----------------------------------------------------------------
// REQUEST / RESPONSE MODELS
// ----------------------------------------------------------------

public class LoginRequest
{
    public string  SchoolId  { get; set; } = string.Empty;
    public string  Password  { get; set; } = string.Empty;
    public string? Email     { get; set; }
    public string? StudentId { get; set; }
    public string? Role      { get; set; }
}

public class ForgotPasswordRequest
{
    public string StudentId { get; set; } = string.Empty;
}

public class VerifyCodeRequest
{
    public string StudentId { get; set; } = string.Empty;
    public string Code      { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token       { get; set; } = string.Empty;
    public string StudentId   { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string Token     { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
}

public class UpdateAccountStatusRequest
{
    public int    AccountId { get; set; }
    public string Status    { get; set; } = string.Empty;
}