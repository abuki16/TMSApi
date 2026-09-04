using System.Text.Json.Serialization;
using TmsApi.Application.DTOs;
namespace TmsApi.Application.DTOs;

public record IssueCertificateRequest(
    int StudentId,
    int CourseId,
    string SerialNumber,
    decimal? Grade = null
);

public record CertificateResponseDto(
    int Id,
    string SerialNumber,
    DateTime IssuedAt,
    int StudentId,
    string StudentName,
    int CourseId,
    string CourseTitle,
    List<LinkDto>? Links = null,
    decimal? GPA = null,
    decimal? Grade = null
)
{
    // This backing property guarantees that if Links is null OR empty, it's ignored completely!
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkDto>? Links { get; init; } = Links is { Count: > 0 } ? Links : null;
}