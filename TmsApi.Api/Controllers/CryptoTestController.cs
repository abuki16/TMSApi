using Microsoft.AspNetCore.Mvc;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CryptoTestController : ControllerBase
{
    [HttpGet("test-salts")]
    public IActionResult TestSalts()
    {
        var service = new CryptoDemoService();
        string hash1 = service.HashUserPassword("Password123!");
        string hash2 = service.HashUserPassword("Password123!");

        // hash1 and hash2 are completely different strings because of unique random salts![cite: 1]
        System.Console.WriteLine($"Hash 1: {hash1}");
        System.Console.WriteLine($"Hash 2: {hash2}");

        // Both verify to true against the same plain text:[cite: 1]
        bool match1 = service.VerifyUserPassword("Password123!", hash1); // true[cite: 1]
        bool match2 = service.VerifyUserPassword("Password123!", hash2); // true[cite: 1]

        return Ok(new
        {
            Hash1 = hash1,
            Hash2 = hash2,
            Match1 = match1,
            Match2 = match2,
            Note = "Decision Rule: Never write custom hashing algorithms in production. In Exercise 2 onwards, you will use UserManager provided by ASP.NET Core Identity."
        });
    }
}