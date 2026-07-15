using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Certificates")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CertificatesController : ControllerBase
{
    private readonly CertificateService _certificateService;

    public CertificatesController(CertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Issue a new certificate")]
    [EndpointDescription("Registers an academic certificate. HATEOAS links are omitted from this initial response.")]
    public async Task<IActionResult> Issue([FromBody] IssueCertificateRequest request)
    {
        var result = await _certificateService.IssueCertificateAsync(request);
        if (result == null)
        {
            return BadRequest("Could not issue certificate. Ensure the student and course exist, and the serial number is unique.");
        }

        // The 'Links' property remains null on creation, so it is completely omitted from the JSON payload.
        return CreatedAtRoute("GetCertificateById", new { id = result.Id }, result);
    }

   [HttpGet("{id:int}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get certificate by ID")]
    [EndpointDescription("Retrieves a certificate record and appends available hypermedia links.")]
    public async Task<IActionResult> GetById(int id)
    {
        var certificate = await _certificateService.GetByIdAsync(id);
        if (certificate == null) return NotFound();

        // Populate links exclusively for GET requests
        var certificateWithLinks = PopulateLinks(certificate);
        return Ok(certificate);
    }

    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(IReadOnlyList<CertificateResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List student certificates")]
    [EndpointDescription("Retrieves all certificates issued to a student, populated with individual action links.")]
    public async Task<IActionResult> GetByStudent(int studentId)
    {
        var certificates = await _certificateService.GetByStudentIdAsync(studentId);
        
        // Map over the results to populate links for each certificate in the list
        var certificatesWithLinks = certificates.Select(PopulateLinks).ToList();
        
        return Ok(certificatesWithLinks);
    }

    
    [HttpDelete("{id}", Name = "RevokeCertificate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Revoke a certificate")]
    [EndpointDescription("Permanently revokes an active certificate. This action is destructive and cannot be undone.")]
    public async Task<IActionResult> Revoke(int id)
    {
        var success = await _certificateService.RevokeCertificateAsync(id);
        if (!success) return NotFound();

        return NoContent();
    }

    // Helper method using C# 'with' expression for safe, immutable copy mutation
    private CertificateResponseDto PopulateLinks(CertificateResponseDto dto)
    {
        var links = new List<LinkDto>
        {
            new(Url.Link("GetCertificateById", new { id = dto.Id }) ?? "", "self", "GET"),
            new(Url.Link("RevokeCertificate", new { id = dto.Id }) ?? "", "revoke", "DELETE")
        };

        return dto with { Links = links };
    }
}