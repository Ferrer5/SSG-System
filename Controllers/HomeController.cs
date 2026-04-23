using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using MyMvcApp.Services;
using MyMvcApp.Data;

namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly IAuthService _authService;

    public HomeController(IAuthService authService)
    {
        _authService = authService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Dashboard()
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

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Json(new { success = true, message = "Logged out successfully" });
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
