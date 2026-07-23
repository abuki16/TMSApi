using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/transcripts")]
[ApiVersion("2.0")]
[Tags("Transcripts")]
[Produces("application/json")]
public class TranscriptsController : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("Request a transcript (Stub)")]
    [EndpointDescription("Stub endpoint for transcript generation to support concurrency limiter measurements.")]
    public IActionResult RequestTranscript([FromBody] object? _)
    {
        // Stub: Exercise 5 swaps this for enqueue + 202 + Location.
        return Ok(new { message = "Transcript request received (stub)." });
    }
}