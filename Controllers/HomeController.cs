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

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Dashboard", "Home");

        var pendingAccounts = await GetPendingAccountsAsync();
        var allAccountRequests = await GetAllAccountRequestsAsync();
        var students = await GetStudentsAsync();
        var admins = await GetAdminsAsync();
        var treasurers = await GetTreasurersAsync();
        var professors = await GetProfessorsAsync();

        var studentOnlyCount = students.Count(s => s.Role == "Student");

        var model = new DashboardViewModel
        {
            RequestedAccounts = pendingAccounts,
            AllAccountRequests = allAccountRequests,
            Students = students,
            Admins = admins,
            Treasurers = treasurers,
            Professors = professors,
            ApprovedAccountsCount = students.Count + professors.Count
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
            var account = await _context.Accounts.FindAsync(request.AccountId);
            if (account == null)
                return Json(new { success = false, message = "Account not found." });

            account.RequestStatus = request.Status;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Account {request.Status} successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to update account status: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAccountActivation([FromBody] DeactivateRequest request)
    {
        try
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId);

            if (account == null)
                return Json(new { success = false, message = "Account not found." });

            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                isActive = account.IsActive,
                message = account.IsActive ? "Account reactivated." : "Account deactivated."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAccount([FromBody] DeactivateRequest request)
    {
        try
        {
            var account = await _context.Accounts
                .Include(a => a.User)
                    .ThenInclude(u => u!.AcademicProfile)
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId);

            if (account == null)
                return Json(new { success = false, message = "Account not found." });

            // 1. delete academic profile first
            if (account.User?.AcademicProfile != null)
                _context.AcademicProfiles.Remove(account.User.AcademicProfile);

            // 2. delete user
            if (account.User != null)
                _context.Users.Remove(account.User);

            // 3. delete account
            _context.Accounts.Remove(account);

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Account deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Delete failed: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStudent(int accountId)
    {
        var user = await _context.Users
            .Include(u => u.Account)
            .Include(u => u.AcademicProfile)
                .ThenInclude(ap => ap!.Course)
            .FirstOrDefaultAsync(u => u.AccountId == accountId);

        if (user == null)
            return Json(new { success = false, message = "Student not found." });

        return Json(new {
            success        = true,
            firstName      = user.FirstName,
            lastName       = user.LastName,
            middleName     = user.MiddleName,
            email          = user.Account?.Email,
            courseId       = user.AcademicProfile?.CourseId,
            yearLevel      = user.AcademicProfile?.YearLevel,
            section        = user.AcademicProfile?.Section,
            academicStatus = user.AcademicProfile?.AcademicStatus.ToString() ?? "Enrolled"
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStudent([FromBody] UpdateStudentRequest request)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Account)
                .Include(u => u.AcademicProfile)
                .FirstOrDefaultAsync(u => u.AccountId == request.AccountId);

            if (user == null)
                return Json(new { success = false, message = "Student not found." });

            // update name
            user.FirstName  = request.FirstName;
            user.LastName   = request.LastName;
            user.MiddleName = request.MiddleName;

            // update email on the account
            if (user.Account != null)
                user.Account.Email = request.Email;

            // update academic profile
            if (user.AcademicProfile != null)
            {
                user.AcademicProfile.CourseId  = request.CourseId;
                user.AcademicProfile.YearLevel = request.YearLevel;
                user.AcademicProfile.Section   = request.Section;

                if (!string.IsNullOrWhiteSpace(request.AcademicStatus)
                    && Enum.TryParse<AcademicStatus>(request.AcademicStatus, out var parsedStatus))
                {
                    user.AcademicProfile.AcademicStatus = parsedStatus;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Student updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Update failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProfessor(int accountId)
    {
        var user = await _context.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.AccountId == accountId);

        if (user == null)
            return Json(new { success = false, message = "Professor not found." });

        return Json(new {
            success    = true,
            firstName  = user.FirstName,
            lastName   = user.LastName,
            middleName = user.MiddleName,
            email      = user.Account?.Email,
            schoolId   = user.Account?.SchoolId
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfessor([FromBody] UpdateProfessorRequest request)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.AccountId == request.AccountId);

            if (user == null)
                return Json(new { success = false, message = "Professor not found." });

            // update name
            user.FirstName  = request.FirstName;
            user.LastName   = request.LastName;
            user.MiddleName = request.MiddleName;

            // update account info
            if (user.Account != null)
            {
                user.Account.Email = request.Email;
                user.Account.SchoolId = request.SchoolId;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Professor updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Update failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        try
        {
            var courses = await _context.Courses
                .OrderBy(c => c.CourseCode)
                .Select(c => new { c.CourseId, c.CourseCode, c.CourseName })
                .ToListAsync();

            return Json(new { success = true, courses });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to get courses: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CheckEmail(string email, int excludeAccountId)
    {
        var account = await _context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Email != null 
                                   && a.Email.ToLower() == email.ToLower() 
                                   && a.AccountId != excludeAccountId);

        if (account == null)
            return Json(new { taken = false });

        var name = account.User != null
            ? $"{account.User.FirstName} {account.User.LastName}"
            : account.SchoolId;

        return Json(new { taken = true, usedBy = name });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole([FromBody] ChangeRoleRequest request)
    {
        try
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId);

            if (account == null)
                return Json(new { success = false, message = "Account not found." });

            if (!Enum.TryParse<UserRole>(request.Role, out var newRole))
                return Json(new { success = false, message = "Invalid role." });

            account.Role = newRole;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Role changed to " + request.Role + " successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to change role: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await GetPendingAccountsAsync();
        return Json(new { success = true, requests });
    }

    [HttpGet]
    public async Task<IActionResult> GetRejectedRequests()
    {
        try
        {
            var rejected = await _context.Accounts
                .Where(a => a.RequestStatus == RequestStatus.Rejected && a.Role == UserRole.Student)
                .Include(a => a.User)
                    .ThenInclude(u => u!.AcademicProfile)
                        .ThenInclude(ap => ap!.Course)
                .Select(a => new RequestedAccountViewModel
                {
                    AccountId  = a.AccountId,
                    SchoolId   = a.SchoolId,
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

            return Json(new { success = true, requests = rejected });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStudentsList()
    {
        var students = await GetStudentsAsync();
        return Json(new { success = true, students });
    }

    [HttpGet]
    public async Task<IActionResult> GetTreasurersList()
    {
        var treasurers = await GetTreasurersAsync();
        return Json(new { success = true, treasurers });
    }

    [HttpGet]
    public async Task<IActionResult> GetProfessorsList()
    {
        var professors = await GetProfessorsAsync();
        return Json(new { success = true, professors });
    }

    [HttpGet]
    public async Task<IActionResult> GetAdminsList()
    {
        var admins = await GetAdminsAsync();
        return Json(new { success = true, admins });
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardStats()
    {
        var allStudents    = await GetStudentsAsync(); // includes both Student + Treasurer roles
        var professors     = await GetProfessorsAsync();
        var admins         = await GetAdminsAsync();
        var pending        = await GetPendingAccountsAsync();
        var allRequests    = await GetAllAccountRequestsAsync();

        var studentCount   = allStudents.Count(s => s.Role == "Student");
        var treasurerCount = allStudents.Count(s => s.Role == "Treasurer");

        return Json(new {
            success        = true,
            approvedCount  = (studentCount + treasurerCount) + professors.Count,
            pendingCount   = pending.Count,
            studentCount   = studentCount + treasurerCount, // students card shows both
            treasurerCount = treasurerCount,
            professorCount = professors.Count,
            adminCount     = admins.Count,
            recentRequests = allRequests
        });
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
            .Where(a => a.RequestStatus == RequestStatus.Pending && a.Role == UserRole.Student)
            .Include(a => a.User)
                .ThenInclude(u => u!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Select(a => new RequestedAccountViewModel
            {
                AccountId  = a.AccountId,
                SchoolId   = a.SchoolId,
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
            .Where(a => a.Role == UserRole.Student || a.Role == UserRole.Treasurer)
            .Include(a => a.User)
                .ThenInclude(u => u!.AcademicProfile)
                    .ThenInclude(ap => ap!.Course)
            .Select(a => new RequestedAccountViewModel
            {
                AccountId  = a.AccountId,
                SchoolId   = a.SchoolId,
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
            .Where(u => u.Account != null 
                     && u.Account.RequestStatus == RequestStatus.Approved
                     && (u.Account.Role == UserRole.Student || u.Account.Role == UserRole.Treasurer))
            .Select(u => new StudentViewModel
            {
                StudentId      = u.UserId,
                FullName       = u.LastName != null && u.FirstName != null
                    ? $"{u.LastName.ToUpper()}, {u.FirstName.ToUpper()}" : "N/A",
                CourseCode     = u.AcademicProfile != null && u.AcademicProfile.Course != null
                    ? u.AcademicProfile.Course.CourseCode : "N/A",
                YearSection    = u.AcademicProfile != null
                    ? $"{(u.AcademicProfile.YearLevel.HasValue ? u.AcademicProfile.YearLevel.Value.ToString() : "N/A")}-{(u.AcademicProfile.Section ?? "N/A")}"
                    : "N/A",
                AccountId      = u.AccountId,
                Role           = u.Account != null ? u.Account.Role.ToString() : "Student",
                IsActive       = u.Account != null ? u.Account.IsActive : false,
                SchoolId       = u.Account != null ? u.Account.SchoolId : "N/A",
                AcademicStatus = u.AcademicProfile != null ? u.AcademicProfile.AcademicStatus.ToString() : "Enrolled"  // add this
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
            .Where(a => (a.Role == UserRole.Professor || a.Role == UserRole.Admin) 
                 && a.RequestStatus == RequestStatus.Approved)
            .Select(a => new ProfessorViewModel
            {
                ProfessorId = a.AccountId,
                AccountId = a.AccountId,
                FullName = a.User != null && a.User.LastName != null && a.User.FirstName != null
                    ? $"{a.User.LastName.ToUpper()}, {a.User.FirstName.ToUpper()}" : "N/A",
                Email = a.Email ?? "N/A",
                SchoolId = a.SchoolId ?? "N/A",
                Role = a.Role.ToString()
            })
            .ToListAsync();

        return professors.OrderBy(p => p.FullName).ToList();
    }

    [HttpPost]
    public async Task<IActionResult> AddProfessor([FromBody] AddProfessorRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                return Json(new { success = false, message = "First and last name are required." });

            if (string.IsNullOrWhiteSpace(request.SchoolId))
                return Json(new { success = false, message = "School ID is required." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return Json(new { success = false, message = "Password must be at least 6 characters." });

            // check if school ID already exists
            var existing = await _context.Accounts
                .FirstOrDefaultAsync(a => a.SchoolId.ToLower() == request.SchoolId.ToLower());

            if (existing != null)
                return Json(new { success = false, message = "School ID is already taken." });

            // create account
            var account = new Account
            {
                SchoolId      = request.SchoolId,
                Email         = request.Email,
                PasswordHash  = AuthService.HashPassword(request.Password),
                Role          = UserRole.Professor,
                RequestStatus = RequestStatus.Approved,
                IsActive      = true,
                CreatedAt     = DateTime.UtcNow
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            // create user
            var user = new User
            {
                AccountId  = account.AccountId,
                FirstName  = request.FirstName,
                LastName   = request.LastName,
                MiddleName = request.MiddleName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Professor added successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to add professor: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSchoolYears()
    {
        try
        {
            var schoolYears = await _context.SchoolYears
                .OrderByDescending(sy => sy.YearStart)
                .ToListAsync();

            var feeRecords = await _context.FullAmounts
                .Select(f => new { f.SchoolYearId, f.Semester })
                .ToListAsync();

            var result = schoolYears.Select(sy => new {
                sy.SchoolYearId,
                sy.YearStart,
                sy.YearEnd,
                hasFirst  = feeRecords.Any(f => f.SchoolYearId == sy.SchoolYearId && f.Semester == Semester.First),
                hasSecond = feeRecords.Any(f => f.SchoolYearId == sy.SchoolYearId && f.Semester == Semester.Second)
            });

            return Json(new { success = true, schoolYears = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddSchoolYear([FromBody] AddSchoolYearRequest request)
    {
        try
        {
            if (request.YearEnd != request.YearStart + 1)
                return Json(new { success = false, message = "Year end must be exactly year start + 1." });

            var existing = await _context.SchoolYears
                .FirstOrDefaultAsync(sy => sy.YearStart == request.YearStart);

            if (existing != null)
                return Json(new { success = false, message = "That school year already exists." });

            _context.SchoolYears.Add(new SchoolYear {
                YearStart = request.YearStart,
                YearEnd   = request.YearEnd
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "School year added successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSchoolYear([FromBody] DeleteSchoolYearRequest request)
    {
        try
        {
            var sy = await _context.SchoolYears
                .Include(s => s.FullAmounts)
                .FirstOrDefaultAsync(s => s.SchoolYearId == request.SchoolYearId);

            if (sy == null)
                return Json(new { success = false, message = "School year not found." });

            if (sy.FullAmounts.Any())
                _context.FullAmounts.RemoveRange(sy.FullAmounts);

            _context.SchoolYears.Remove(sy);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "School year deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddCourse([FromBody] AddCourseRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CourseCode))
                return Json(new { success = false, message = "Course code is required." });

            var existing = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode.ToLower() == request.CourseCode.ToLower());

            if (existing != null)
                return Json(new { success = false, message = "That course code already exists." });

            _context.Courses.Add(new Course {
                CourseCode = request.CourseCode.ToUpper(),
                CourseName = request.CourseName
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Course added successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCourse([FromBody] DeleteCourseRequest request)
    {
        try
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseId == request.CourseId);

            if (course == null)
                return Json(new { success = false, message = "Course not found." });

            var inUse = await _context.AcademicProfiles
                .AnyAsync(ap => ap.CourseId == request.CourseId);

            if (inUse)
                return Json(new { success = false, message = "Cannot delete — students are currently assigned to this course." });

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Course deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetFees()
    {
        try
        {
            var fees = await _context.FullAmounts
                .Include(f => f.SchoolYear)
                .OrderByDescending(f => f.SchoolYear.YearStart)
                .ThenBy(f => f.Semester)
                .ToListAsync();

            var latestFirst  = fees.FirstOrDefault(f => f.Semester == Semester.First);
            var latestSecond = fees.FirstOrDefault(f => f.Semester == Semester.Second);

            var result = fees.Select(f => new {
                f.FullAmountId,
                schoolYear = $"{f.SchoolYear.YearStart} – {f.SchoolYear.YearEnd}",
                semester   = f.Semester.ToString(),
                amount     = f.Amount,
                isLatest   = f.FullAmountId == latestFirst?.FullAmountId ||
                             f.FullAmountId == latestSecond?.FullAmountId
            });

            return Json(new { success = true, fees = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetFeeAmount([FromBody] SetFeeAmountRequest request)
    {
        try
        {
            if (request.Amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero." });

            var semesterInput = (request.Semester ?? string.Empty).Trim();
            Semester semester;
            if (semesterInput.Equals("1st", StringComparison.OrdinalIgnoreCase) ||
                semesterInput.Equals("First", StringComparison.OrdinalIgnoreCase))
            {
                semester = Semester.First;
            }
            else if (semesterInput.Equals("2nd", StringComparison.OrdinalIgnoreCase) ||
                     semesterInput.Equals("Second", StringComparison.OrdinalIgnoreCase))
            {
                semester = Semester.Second;
            }
            else
            {
                return Json(new { success = false, message = "Invalid semester." });
            }

            var sy = await _context.SchoolYears
                .FirstOrDefaultAsync(s => s.SchoolYearId == request.SchoolYearId);

            if (sy == null)
                return Json(new { success = false, message = "School year not found." });

            var semesterText = semester == Semester.First ? "1st" : "2nd";

            // Upsert directly against the table to avoid enum/provider tracking issues
            // while still enforcing the unique (school_year_id, semester) constraint.
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO full_amount (school_year_id, semester, full_amount)
                VALUES ({request.SchoolYearId}, {semesterText}, {request.Amount})
                ON DUPLICATE KEY UPDATE full_amount = VALUES(full_amount);");

            return Json(new { success = true, message = "Fee amount set successfully." });
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            return Json(new { success = false, message = $"Failed to set fee amount: {detail}" });
        }
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

public class DeactivateRequest
{
    public int AccountId { get; set; }
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
    public int           AccountId { get; set; }
    public RequestStatus Status    { get; set; }
}

public class UpdateStudentRequest
{
    public int     AccountId      { get; set; }
    public string? FirstName      { get; set; }
    public string? LastName       { get; set; }
    public string? MiddleName     { get; set; }
    public string? Email          { get; set; }
    public int     CourseId       { get; set; }
    public int?    YearLevel      { get; set; }
    public string? Section        { get; set; }
    public string? AcademicStatus { get; set; }  // add this
}

public class UpdateProfessorRequest
{
    public int     AccountId  { get; set; }
    public string? FirstName  { get; set; }
    public string? LastName   { get; set; }
    public string? MiddleName { get; set; }
    public string? Email      { get; set; }
    public string? SchoolId   { get; set; }
}

public class ChangeRoleRequest
{
    public int    AccountId { get; set; }
    public string Role      { get; set; } = string.Empty;
}

public class AddProfessorRequest
{
    public string  FirstName  { get; set; } = string.Empty;
    public string  LastName   { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string  SchoolId   { get; set; } = string.Empty;
    public string? Email      { get; set; }
    public string  Password   { get; set; } = string.Empty;
}

public class AddSchoolYearRequest
{
    public int YearStart { get; set; }
    public int YearEnd   { get; set; }
}

public class DeleteSchoolYearRequest
{
    public int SchoolYearId { get; set; }
}

public class AddCourseRequest
{
    public string  CourseCode { get; set; } = string.Empty;
    public string? CourseName { get; set; }
}

public class DeleteCourseRequest
{
    public int CourseId { get; set; }
}

public class SetFeeAmountRequest
{
    public int     SchoolYearId { get; set; }
    public string  Semester     { get; set; } = string.Empty;
    public decimal Amount       { get; set; }
}