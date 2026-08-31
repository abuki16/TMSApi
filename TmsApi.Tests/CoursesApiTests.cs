using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace TmsApi.Tests;

public class CoursesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CoursesApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        
        var token = GenerateJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetAllCourses_ReturnsOkAndPagedResponse()
    {
        // Act - Notice the versioned route path /api/v1/courses
        var response = await _client.GetAsync("/api/v1/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        // Verify it returns the paged envelope object containing "items"
        Assert.True(root.TryGetProperty("items", out var itemsElement));
        Assert.Equal(JsonValueKind.Array, itemsElement.ValueKind);
        Assert.True(itemsElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetCourseById_WithValidId_ReturnsCourseDetails()
    {
        // Fetch the list first to grab a valid integer ID
        var listResponse = await _client.GetAsync("/api/v1/courses");
        var jsonResponse = await listResponse.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(jsonResponse);
        var firstItem = doc.RootElement.GetProperty("items")[0];
        int courseId = firstItem.GetProperty("id").GetInt32();

        // Act - Request the course by its integer ID
        var response = await _client.GetAsync($"/api/v1/courses/{courseId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_WithEmptyCode_ReturnsBadRequest()
    {
        var invalidCourseDto = new
        {
            Code = "", 
            Title = "Invalid Course",
            MaxCapacity = 30
        };

        var response = await _client.PostAsJsonAsync("/api/v1/courses", invalidCourseDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string GenerateJwtToken(string role)
    {
        var secretKey = "SuperSecretJwtKeyForTmsApiDevelopment2026!@#$%";
        var key = Encoding.UTF8.GetBytes(secretKey);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-admin-id"),
            new Claim(ClaimTypes.Name, "Test Admin"),
            new Claim(ClaimTypes.Role, role)
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
}