using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Issue a new certificate")]
    [EndpointDescription("Registers an academic certificate. HATEOAS links are omitted from this initial response.")]
    public async Task<IActionResult> Issue([FromBody] IssueCertificateRequest request)
    {
        try
        {
            var result = await _certificateService.IssueCertificateAsync(request);
            
            // Fixed: Use CreatedAtAction pointing to GetById method safely
            return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            // Returns a 400 Bad Request with the specific duplicate message
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            // Returns a 404 Not Found if the student or course is missing
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}", Name = "GetCertificateById")]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get certificate by ID")]
    [EndpointDescription("Retrieves a certificate record and appends available hypermedia links.")]
    public async Task<IActionResult> GetById(int id)
    {
        var certificate = await _certificateService.GetByIdAsync(id);
        if (certificate == null) return NotFound();

        var certificateWithLinks = PopulateLinks(certificate);
        return Ok(certificateWithLinks);
    }

    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(IReadOnlyList<CertificateResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List student certificates")]
    [EndpointDescription("Retrieves all certificates issued to a student, populated with individual action links.")]
    public async Task<IActionResult> GetByStudent(int studentId)
    {
        var certificates = await _certificateService.GetByStudentIdAsync(studentId);
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