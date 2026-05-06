using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MyMvcApp.Models;
using MyMvcApp.Services;
using MyMvcApp.Data;
using MyMvcApp.Filters;

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

    // ── STUDENT DASHBOARD ──
    [ServiceFilter(typeof(AuthFilter))]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Dashboard()
    {
        var role = HttpContext.Session.GetString("UserRole");

        if (role == "Admin")
            return RedirectToAction("AdminDashboard", "Home");

        if (role == "Treasurer")
            return RedirectToAction("TreasurerDashboard", "Home");

        if (role == "Professor")
            return RedirectToAction("ProfessorDashboard", "Home");

        var pendingAccounts = await GetPendingAccountsAsync();
        var students        = await GetStudentsAsync();

        var model = new DashboardViewModel
        {
            RequestedAccounts = pendingAccounts,
            Students          = students
        };

        return View("~/Views/Dashboard/student_dashboard.cshtml", model);
    }

    // ── ADMIN DASHBOARD ──
    [ServiceFilter(typeof(AuthFilter))]
    [TypeFilter(typeof(RoleFilter), Arguments = new object[] { new[] { "Admin" } })]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> AdminDashboard()
    {
        var pendingAccounts    = await GetPendingAccountsAsync();
        var allAccountRequests = await GetAllAccountRequestsAsync();
        var students           = await GetStudentsAsync();
        var admins             = await GetAdminsAsync();
        var treasurers         = await GetTreasurersAsync();
        var professors         = await GetProfessorsAsync();

        var studentOnlyCount = students.Count(s => s.Role == "Student");

        var model = new DashboardViewModel
        {
            RequestedAccounts     = pendingAccounts,
            AllAccountRequests    = allAccountRequests,
            Students              = students,
            Admins                = admins,
            Treasurers            = treasurers,
            Professors            = professors,
            ApprovedAccountsCount = studentOnlyCount + treasurers.Count + professors.Count + admins.Count
        };

        return View("~/Views/Dashboard/admin_dashboard.cshtml", model);
    }

    // ── TREASURER DASHBOARD ──
    [ServiceFilter(typeof(AuthFilter))]
    [TypeFilter(typeof(RoleFilter), Arguments = new object[] { new[] { "Treasurer" } })]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult TreasurerDashboard()
    {
        return View("~/Views/Dashboard/treasurer_dashboard.cshtml");
    }

    // ── PROFESSOR DASHBOARD ──
    [ServiceFilter(typeof(AuthFilter))]
    [TypeFilter(typeof(RoleFilter), Arguments = new object[] { new[] { "Professor" } })]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ProfessorDashboard()
    {
        return View("~/Views/Dashboard/professor_dashboard.cshtml");
    }

    // ----------------------------------------------------------------
    // LOGIN
    // ----------------------------------------------------------------

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("login")]
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
            // Log the real error, show safe message to user
            Console.WriteLine($"Login failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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

    // ── ADD THESE TWO BACK ──────────────────────────────────────
    [HttpGet]
    public IActionResult CheckAuth()
    {
        var isAuthenticated = HttpContext.Session.GetString("UserId") != null;
        return Json(new { authenticated = isAuthenticated });
    }

    [HttpGet]
    public IActionResult ClearSession()
    {
        HttpContext.Session.Clear();
        return Json(new { success = true });
    }
    // ────────────────────────────────────────────────────────────

    // ----------------------------------------------------------------
    // REGISTER
    // ----------------------------------------------------------------

    // ── REGISTER — add these checks before calling _authService.RegisterAccountAsync ──
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        try
        {
            if (request == null)
                return Json(new { success = false, message = "Registration request is null." });

            // ── LENGTH VALIDATION ──
            if (string.IsNullOrWhiteSpace(request.SchoolId) || request.SchoolId.Length > 20)
                return Json(new { success = false, message = "School ID must be between 1 and 20 characters." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6 || request.Password.Length > 100)
                return Json(new { success = false, message = "Password must be between 6 and 100 characters." });

            if (!string.IsNullOrWhiteSpace(request.FirstName) && request.FirstName.Length > 50)
                return Json(new { success = false, message = "First name must not exceed 50 characters." });

            if (!string.IsNullOrWhiteSpace(request.LastName) && request.LastName.Length > 50)
                return Json(new { success = false, message = "Last name must not exceed 50 characters." });

            if (!string.IsNullOrWhiteSpace(request.MiddleName) && request.MiddleName.Length > 50)
                return Json(new { success = false, message = "Middle name must not exceed 50 characters." });

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (request.Email.Length > 100)
                    return Json(new { success = false, message = "Email must not exceed 100 characters." });

                if (!IsValidEmail(request.Email))
                    return Json(new { success = false, message = "Invalid email format." });
            }

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
            Console.WriteLine($"Registration failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // ----------------------------------------------------------------
    // FORGOT PASSWORD
    // ----------------------------------------------------------------

    [HttpPost]
    [IgnoreAntiforgeryToken]
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
            Console.WriteLine($"Forgot password failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // ----------------------------------------------------------------
    // VERIFY RESET CODE
    // ----------------------------------------------------------------

    [HttpPost]
    [IgnoreAntiforgeryToken]
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
            Console.WriteLine($"Verify code failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
    [IgnoreAntiforgeryToken]
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
            Console.WriteLine($"Reset password failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            Console.WriteLine($"Update account status failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            Console.WriteLine($"Toggle activation failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            Console.WriteLine($"Delete account failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            success    = true,
            firstName  = user.FirstName,
            lastName   = user.LastName,
            middleName = user.MiddleName,
            email      = user.Account?.Email,
            courseId   = user.AcademicProfile?.CourseId,
            yearLevel  = user.AcademicProfile?.YearLevel,
            section    = user.AcademicProfile?.Section
        });
    }

    // ── UPDATE STUDENT — add these checks at the top ──
    [HttpPost]
    public async Task<IActionResult> UpdateStudent([FromBody] UpdateStudentRequest request)
    {
        try
        {
            // ── LENGTH VALIDATION ──
            if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 50)
                return Json(new { success = false, message = "First name must be between 1 and 50 characters." });

            if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length > 50)
                return Json(new { success = false, message = "Last name must be between 1 and 50 characters." });

            if (!string.IsNullOrWhiteSpace(request.MiddleName) && request.MiddleName.Length > 50)
                return Json(new { success = false, message = "Middle name must not exceed 50 characters." });

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (request.Email.Length > 100)
                    return Json(new { success = false, message = "Email must not exceed 100 characters." });

                if (!IsValidEmail(request.Email))
                    return Json(new { success = false, message = "Invalid email format." });
            }

            if (!string.IsNullOrWhiteSpace(request.Section) && request.Section.Length > 10)
                return Json(new { success = false, message = "Section must not exceed 10 characters." });

            if (request.YearLevel.HasValue && (request.YearLevel < 1 || request.YearLevel > 4))
                return Json(new { success = false, message = "Year level must be between 1 and 4." });

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
                user.AcademicProfile.CourseId   = request.CourseId;
                user.AcademicProfile.YearLevel  = request.YearLevel;
                user.AcademicProfile.Section    = request.Section;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Student updated successfully." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update student failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

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
            Console.WriteLine($"Get courses failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            Console.WriteLine($"Change role failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            Console.WriteLine($"Get rejected requests failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            approvedCount  = studentCount + treasurerCount + professors.Count + admins.Count,
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
            Console.WriteLine($"Test DB failed: {ex}");
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
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
            .Where(a => a.Role == UserRole.Student)
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
                StudentId   = u.UserId,
                FullName    = u.LastName != null && u.FirstName != null
                    ? $"{u.LastName.ToUpper()}, {u.FirstName.ToUpper()}" : "N/A",
                CourseCode  = u.AcademicProfile != null && u.AcademicProfile.Course != null
                    ? u.AcademicProfile.Course.CourseCode : "N/A",
                YearSection = u.AcademicProfile != null
                    ? $"{(u.AcademicProfile.YearLevel.HasValue ? u.AcademicProfile.YearLevel.Value.ToString() : "N/A")}-{(u.AcademicProfile.Section ?? "N/A")}"
                    : "N/A",
                AccountId   = u.AccountId,
                Role        = u.Account != null ? u.Account.Role.ToString() : "Student",
                IsActive    = u.Account != null ? u.Account.IsActive : false
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
                FullName = a.User != null && a.User.LastName != null && a.User.FirstName != null
                    ? $"{a.User.LastName.ToUpper()}, {a.User.FirstName.ToUpper()}" : "N/A",
                Email = a.Email ?? "N/A",
                SchoolId = a.SchoolId ?? "N/A",
                Role = a.Role.ToString()
            })
            .ToListAsync();

        return professors.OrderBy(p => p.FullName).ToList();
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
    public int     AccountId  { get; set; }
    public string? FirstName  { get; set; }
    public string? LastName   { get; set; }
    public string? MiddleName { get; set; }
    public string? Email      { get; set; }
    public int     CourseId   { get; set; }
    public int?    YearLevel  { get; set; }
    public string? Section    { get; set; }
}

public class ChangeRoleRequest
{
    public int    AccountId { get; set; }
    public string Role      { get; set; } = string.Empty;
}