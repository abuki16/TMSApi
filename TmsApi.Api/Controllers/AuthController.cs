using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers;

[EnableRateLimiting("AuthLimiter")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly TmsDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager,
        TmsDbContext context,
        TokenService tokenService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenService = tokenService;
    }

    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Role,
        int? AssignedCourseId = null);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Ok(new { message = "Registration request received." });
        }

        var user = new TmsUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole(request.Role));
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        if (request.Role == "Instructor" && request.AssignedCourseId.HasValue)
        {
            var course = await _context.Courses.FindAsync(request.AssignedCourseId.Value);
            if (course != null)
            {
                course.InstructorId = user.Id;
                await _context.SaveChangesAsync();
            }
        }

        if (request.Role == "Student")
        {
            var studentName = $"{request.FirstName} {request.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(studentName)) studentName = request.Email;

            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Name.ToLower() == studentName.ToLower());
            if (existingStudent == null)
            {
                var count = await _context.Students.IgnoreQueryFilters().CountAsync();
                var newStudent = new Student
                {
                    Name = studentName,
                    RegistrationNumber = $"TMS-{DateTime.UtcNow.Year}-{(count + 1):D4}",
                    GPA = 0.0m,
                    IsActive = true
                };
                _context.Students.Add(newStudent);
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new { message = "Registration successful." });
    }

    public record LoginRequest(string Email, string Password);
     
    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { detail = "Invalid credentials." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return StatusCode(423, new { detail = "Account locked due to multiple failed login attempts. Try again in 15 minutes." });
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { detail = "Invalid credentials." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName)) fullName = user.UserName ?? user.Email ?? "User";

        int? studentId = null;
        if (roles.Contains("Student"))
        {
            var student = await _context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Name.ToLower() == fullName.ToLower());
            if (student != null && (student.IsDeleted || !student.IsActive))
            {
                return Unauthorized(new { detail = "Student account has been deactivated or deleted." });
            }

            if (student == null)
            {
                var count = await _context.Students.IgnoreQueryFilters().CountAsync();
                student = new Student
                {
                    Name = fullName,
                    RegistrationNumber = $"TMS-{DateTime.UtcNow.Year}-{(count + 1):D4}",
                    GPA = 3.8m,
                    IsActive = true
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
            }
            studentId = student.Id;
        }

        var accessToken = _tokenService.GenerateJwt(user, roles, studentId, fullName);

        var refreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            accessToken,
            refreshToken = refreshToken.Token,
            user = new
            {
                id = user.Id,
                email = user.Email,
                displayName = fullName,
                firstName = user.FirstName,
                lastName = user.LastName,
                role = roles.FirstOrDefault() ?? "Student",
                roles,
                studentId
            }
        });
    }

    public record RefreshRequest(string RefreshToken);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (storedToken == null)
        {
            return Unauthorized(new { detail = "Invalid refresh token." });
        }

        if (storedToken.IsUsed)
        {
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId)
                .ToListAsync();

            foreach (var t in userTokens)
            {
                t.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
            return Unauthorized(new { detail = "Token theft detected. All user sessions revoked." });
        }

        if (storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new { detail = "Refresh token expired or revoked." });
        }

        storedToken.IsUsed = true;

        var newRefreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        var roles = await _userManager.GetRolesAsync(user!);
        var fullName = $"{user!.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName)) fullName = user.UserName ?? user.Email ?? "User";

        int? studentId = null;
        if (roles.Contains("Student"))
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Name.ToLower() == fullName.ToLower());
            studentId = student?.Id;
        }

        var newAccessToken = _tokenService.GenerateJwt(user!, roles, studentId, fullName);

        return Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken.Token,
            user = new
            {
                id = user.Id,
                email = user.Email,
                displayName = fullName,
                firstName = user.FirstName,
                lastName = user.LastName,
                role = roles.FirstOrDefault() ?? "Student",
                roles,
                studentId
            }
        });
    }

    // ==========================================
    // User Management Endpoints (List, Update & Delete)
    // ==========================================

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var courses = await _context.Courses.AsNoTracking().ToListAsync();

        var result = new List<object>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? "Student";
            var assignedCourse = courses.FirstOrDefault(c => c.InstructorId == u.Id);

            result.Add(new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.UserName,
                role,
                roles,
                assignedCourseId = assignedCourse?.Id,
                assignedCourseTitle = assignedCourse != null ? $"{assignedCourse.Code} - {assignedCourse.Title}" : null
            });
        }

        return Ok(result);
    }

    public record UpdateUserRequest(string Email, string FirstName, string LastName, string UserName);

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.UserName = request.UserName;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        return NoContent();
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Name.ToLower() == fullName.ToLower());
            if (student != null)
            {
                student.IsDeleted = true;
                student.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        return NoContent();
    }
}