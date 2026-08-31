using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TmsApi.Application.DTOs;

namespace TmsApi.Tests;

public class CoursesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CoursesControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        // Generate a valid JWT token using your exact appsettings.json values
        var token = GenerateJwtToken();

        // Attach the token to the Authorization header
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private string GenerateJwtToken()
    {
        var secretKey = "SuperSecretJwtKeyForTmsApiDevelopment2026!@#$%";
        var key = Encoding.UTF8.GetBytes(secretKey);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test Instructor"),
            new Claim(ClaimTypes.Role, "Admin") // Adjust if your endpoint requires a specific role
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "http://localhost:5049",
            Audience = "tms-client",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task GetAllCourses_ReturnsOkAndSeededCourses()
    {
        // Act
        var response = await _client.GetAsync("/api/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify content type is JSON
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);

        // Read the raw JSON string to inspect its structure
        var jsonResponse = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(jsonResponse));

        // TODO: Once you see the format, deserialize into the correct wrapper or list.
        // If your API wraps responses, e.g., { "data": [ ... ] }, you'll need a wrapper class or dynamic parsing.
    }
}