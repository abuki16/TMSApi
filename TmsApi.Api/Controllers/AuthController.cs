
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Behaviors;
using TmsApi.Application.DTOs;

namespace TmsApi.Api.Controllers;
[ApiController]
[Route("api/{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(
        [FromBody] LoginRequest request,
        [FromServices] IWebHostEnvironment env)
    {
        // Validate credentials (demo account for M10 transport testing)
        if (request.Username == "admin" && request.Password == "Password123!")
        {
            var dummyJwt = "header.payload.signature-demo-token";
            
            // Append HttpOnly authentication cookie — JavaScript CANNOT read this token
            Response.Cookies.Append("tms_auth", dummyJwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(), // HTTPS in prod; HTTP permitted locally over dev
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            // Readable XSRF Cookie for double-submit protection
            var xsrfToken = Guid.NewGuid().ToString();
            Response.Cookies.Append("XSRF-TOKEN", xsrfToken, new CookieOptions
            {
                HttpOnly = false, // MUST be false so Angular JavaScript can read it!
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            return Ok(new UserProfileDto("System Admin", "Admin"));
        }
        
        return Unauthorized(new { detail = "Invalid username or password." });
    }

    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        // Inspect cookie attached automatically by the browser on cross-origin requests
        if (Request.Cookies.TryGetValue("tms_auth", out _))
        {
            return Ok(new UserProfileDto("System Admin", "Admin"));
        }
        
        return Unauthorized(new { detail = "Session expired or missing authentication cookie." });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("tms_auth");
        Response.Cookies.Delete("XSRF-TOKEN");
        
        return Ok(new { message = "Logged out successfully." });
    }
}



// using Microsoft.AspNetCore.Mvc;
// using TmsApi.Application.Behaviors;
// using TmsApi.Application.DTOs;

// namespace TmsApi.Api.Controllers;

// [ApiController]
// [Route("api/auth")] // <--- Simplified clean route
// public class AuthController : ControllerBase
// {
//     [HttpPost("login")]
//     public IActionResult Login(
//         [FromBody] LoginRequest request,
//         [FromServices] IWebHostEnvironment env)
//     {
//         if (request.Username == "admin" && request.Password == "Password123!")
//         {
//             var dummyJwt = "header.payload.signature-demo-token";
            
//             Response.Cookies.Append("tms_auth", dummyJwt, new CookieOptions
//             {
//                 HttpOnly = true,
//                 Secure = !env.IsDevelopment(),
//                 SameSite = SameSiteMode.Strict,
//                 Expires = DateTimeOffset.UtcNow.AddHours(2)
//             });

//             var xsrfToken = Guid.NewGuid().ToString();
//             Response.Cookies.Append("XSRF-TOKEN", xsrfToken, new CookieOptions
//             {
//                 HttpOnly = false,
//                 Secure = !env.IsDevelopment(),
//                 SameSite = SameSiteMode.Strict,
//                 Expires = DateTimeOffset.UtcNow.AddHours(2)
//             });

//             return Ok(new UserProfileDto("System Admin", "Admin"));
//         }
        
//         return Unauthorized(new { detail = "Invalid username or password." });
//     }

//     [HttpGet("me")]
//     public IActionResult GetCurrentUser()
//     {
//         if (Request.Cookies.TryGetValue("tms_auth", out _))
//         {
//             return Ok(new UserProfileDto("System Admin", "Admin"));
//         }
        
//         return Unauthorized(new { detail = "Session expired or missing authentication cookie." });
//     }

//     [HttpPost("logout")]
//     public IActionResult Logout()
//     {
//         Response.Cookies.Delete("tms_auth");
//         Response.Cookies.Delete("XSRF-TOKEN");
        
//         return Ok(new { message = "Logged out successfully." });
//     }
// }