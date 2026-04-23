using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;
using System.Security.Cryptography;
using System.Text;

namespace MyMvcApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AuthResult> AuthenticateAsync(string email, string password, UserRole role)
        {
            try
            {
                // Find account by email and role
                var account = await _context.Accounts
                    .Include(a => a.Student)
                    .ThenInclude(s => s!.AcademicProfile)
                    .FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower() && a.Role == role);

                if (account == null)
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = $"No {role} account found with this email address." 
                    };
                }

                // Check if account is approved
                if (account.RequestStatus != RequestStatus.Approve)
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = $"Account is {account.RequestStatus.ToString().ToLower()}. Please contact administrator." 
                    };
                }

                // Verify password
                if (!VerifyPassword(password, account.PasswordHash))
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = "Invalid password." 
                    };
                }

                // Update online status
                account.IsOnline = true;
                await _context.SaveChangesAsync();

                return new AuthResult 
                { 
                    Success = true, 
                    Message = "Authentication successful.",
                    Account = account,
                    Student = account.Student
                };
            }
            catch (Exception ex)
            {
                return new AuthResult 
                { 
                    Success = false, 
                    Message = $"An error occurred during authentication: {ex.Message}" 
                };
            }
        }

        public async Task<AuthResult> AuthenticateByUsernameAsync(string username, string password, UserRole role)
        {
            try
            {
                // Find account by username and role
                var account = await _context.Accounts
                    .Include(a => a.Student)
                    .ThenInclude(s => s!.AcademicProfile)
                    .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower() && a.Role == role);

                if (account == null)
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = $"No {role} account found with this username." 
                    };
                }

                // Check if account is approved
                if (account.RequestStatus != RequestStatus.Approve)
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = $"Account is {account.RequestStatus.ToString().ToLower()}. Please contact administrator." 
                    };
                }

                // Verify password
                if (!VerifyPassword(password, account.PasswordHash))
                {
                    return new AuthResult 
                    { 
                        Success = false, 
                        Message = "Invalid password." 
                    };
                }

                // Update online status
                account.IsOnline = true;
                await _context.SaveChangesAsync();

                return new AuthResult 
                { 
                    Success = true, 
                    Message = "Authentication successful.",
                    Account = account,
                    Student = account.Student
                };
            }
            catch (Exception ex)
            {
                return new AuthResult 
                { 
                    Success = false, 
                    Message = $"An error occurred during authentication: {ex.Message}" 
                };
            }
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                // For now, using simple comparison. In production, use proper hashing like BCrypt
                // This assumes passwords are stored as plain text for development
                // TODO: Implement proper password hashing
                return password == passwordHash;
            }
            catch
            {
                return false;
            }
        }

        public static string HashPassword(string password)
        {
            // Simple hashing for development - replace with proper hashing in production
            // TODO: Implement proper password hashing with BCrypt or similar
            return password;
        }

        private static string ConvertYearLevelToString(YearLevel? yearLevel)
        {
            return yearLevel switch
            {
                YearLevel.First => "1",
                YearLevel.Second => "2",
                YearLevel.Third => "3",
                YearLevel.Fourth => "4",
                _ => "1" // default fallback
            };
        }

        public async Task<RegistrationResult> RegisterAccountAsync(RegistrationRequest request)
        {
            try
            {
                Console.WriteLine("RegisterAccountAsync called");
                
                // Test database connection
                if (_context == null)
                {
                    Console.WriteLine("Database context is null");
                    return new RegistrationResult 
                    { 
                        Success = false, 
                        Message = "Database context is null." 
                    };
                }

                Console.WriteLine($"Starting registration for: {request?.Username}");

                // Add null check for request
                if (request == null)
                {
                    Console.WriteLine("Registration request is null");
                    return new RegistrationResult 
                    { 
                        Success = false, 
                        Message = "Registration request is null." 
                    };
                }

                Console.WriteLine($"Request details: Username={request.Username}, Email={request.Email}, Role={request.Role}");
                Console.WriteLine($"Student fields: Firstname={request.Firstname}, Lastname={request.Lastname}, CourseCode={request.CourseCode}");

                // Use local variable to avoid null reference warnings
                var regRequest = request;

                // Check if username already exists
                var usernameLower = regRequest.Username?.ToLower() ?? "";
                var existingUsername = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Username.ToLower() == usernameLower);
                
                if (existingUsername != null)
                {
                    return new RegistrationResult 
                    { 
                        Success = false, 
                        Message = "Username already exists." 
                    };
                }

                // Check if email already exists
                var emailLower = regRequest.Email?.ToLower() ?? "";
                var existingEmail = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Email.ToLower() == emailLower);
                
                if (existingEmail != null)
                {
                    return new RegistrationResult 
                    { 
                        Success = false, 
                        Message = "Email already exists." 
                    };
                }

                // Validate student-specific fields if role is Student
                if (regRequest.Role == UserRole.Student)
                {
                    if (string.IsNullOrWhiteSpace(regRequest.Firstname) || 
                        string.IsNullOrWhiteSpace(regRequest.Lastname) ||
                        string.IsNullOrWhiteSpace(regRequest.CourseCode) ||
                        !regRequest.YearLevel.HasValue ||
                        string.IsNullOrWhiteSpace(regRequest.StudentId))
                    {
                        return new RegistrationResult 
                        { 
                            Success = false, 
                            Message = "All student fields are required for student registration." 
                        };
                    }
                }

                // Create account
                var account = new Account
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = HashPassword(request.Password),
                    Role = request.Role,
                    RequestStatus = RequestStatus.Pending, // New accounts need approval
                    IsOnline = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // Create student record if role is Student
                Student? student = null;
                if (request.Role == UserRole.Student)
                {
                    student = new Student
                    {
                        StudentId = int.Parse(request.StudentId ?? "0"),
                        AccountId = account.AccountId,
                        Firstname = request.Firstname,
                        Lastname = request.Lastname,
                        MiddleName = request.MiddleName
                    };

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    // Find course
                    Course? course = null;
                    if (!string.IsNullOrWhiteSpace(request.CourseCode))
                    {
                        course = await _context.Courses
                            .FirstOrDefaultAsync(c => c.CourseCode.ToLower() == request.CourseCode.ToLower());

                        if (course == null)
                        {
                            // Create course if it doesn't exist
                            course = new Course
                            {
                                CourseCode = request.CourseCode,
                                IsActive = true
                            };
                            _context.Courses.Add(course);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // Create a default course if CourseCode is null
                        course = new Course
                        {
                            CourseCode = "DEFAULT",
                            IsActive = true
                        };
                        _context.Courses.Add(course);
                        await _context.SaveChangesAsync();
                    }

                    // Create academic profile
                    var academicProfile = new AcademicProfile
                    {
                        StudentId = student.StudentId,
                        CourseId = course.CourseId,
                        YearLevel = ConvertYearLevelToString(request.YearLevel),
                        Section = request.Section ?? "A", // Default section if null
                        AcademicStatus = AcademicStatus.Enrolled
                    };

                    _context.AcademicProfiles.Add(academicProfile);
                    await _context.SaveChangesAsync();
                }

                return new RegistrationResult 
                { 
                    Success = true, 
                    Message = "Account created successfully. Please wait for admin approval.",
                    Account = account,
                    Student = student
                };
            }
            catch (Exception ex)
            {
                return new RegistrationResult 
                { 
                    Success = false, 
                    Message = $"Registration failed: {ex.Message}" 
                };
            }
        }
    }
}
