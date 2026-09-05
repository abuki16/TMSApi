
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class CertificateService
{
    private readonly TmsDbContext _context; 

    public CertificateService(TmsDbContext context)
    {
        _context = context;
    }

    public async Task<CertificateResponseDto?> IssueCertificateAsync(IssueCertificateRequest request)
{
    // 1. Check if the student has already been issued a certificate for this course
    var alreadyExists = await _context.Certificates
        .AnyAsync(c => c.StudentId == request.StudentId && c.CourseId == request.CourseId);

    if (alreadyExists)
    {
        throw new InvalidOperationException("A certificate has already been issued to this student for this course.");
    }

    // 2. Validation checks for Student and Course existence
    var studentExists = await _context.Students.AnyAsync(s => s.Id == request.StudentId);
    var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId);
    
    if (!studentExists || !courseExists)
    {
        throw new KeyNotFoundException("Student or course record does not exist.");
    }

    // 3. Validation check: Student must be enrolled and finished the course with a submitted grade
    var enrollment = await _context.Enrollments
        .FirstOrDefaultAsync(e => e.StudentId == request.StudentId && e.CourseId == request.CourseId);

    if (enrollment == null)
    {
        throw new InvalidOperationException(
            "Certificate cannot be issued: The student is not enrolled in this course.");
    }

    if (!string.Equals(enrollment.Status, "Approved", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Certificate cannot be issued: The student's enrollment has not been approved by an administrator.");
    }

    // If the administrator provided/verified a grade, update the enrollment grade
    if (request.Grade.HasValue)
    {
        if (request.Grade.Value < 0.0m || request.Grade.Value > 4.0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Grade),
                "Grade must be between 0.00 and 4.00.");
        }
        enrollment.Grade = Math.Round(request.Grade.Value, 2);
    }

    if (enrollment.Grade == null || enrollment.Grade.Value < 2.00m)
    {
        throw new InvalidOperationException("Certificate cannot be issued: The student has not completed this course with a passing grade (minimum 2.00 / Grade C).");
    }

    // 4. Calculate and finalize the student's official registered GPA from all completed graded courses
    var student = await _context.Students
        .Include(s => s.Enrollments)
        .FirstOrDefaultAsync(s => s.Id == request.StudentId);

    if (student != null)
    {
        var graded = student.Enrollments.Where(e => e.Grade.HasValue && e.Grade.Value > 0).ToList();
        if (graded.Count > 0)
        {
            student.GPA = Math.Round(graded.Average(e => e.Grade!.Value), 2);
        }
    }

    // 5. Create and save the certificate if all checks pass
    var certificate = new Certificate
    {
        StudentId = request.StudentId,
        CourseId = request.CourseId,
        SerialNumber = request.SerialNumber,
        IssuedAt = DateTime.UtcNow
    };

    _context.Certificates.Add(certificate);
    await _context.SaveChangesAsync();

    // Retrieve the newly created certificate with relations to construct the response DTO
    return await GetByIdAsync(certificate.Id);
}

    public async Task<CertificateResponseDto?> GetByIdAsync(int id)
    {
        var cert = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cert == null) return null;

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == cert.StudentId && e.CourseId == cert.CourseId);

        return new CertificateResponseDto(
            cert.Id,
            cert.SerialNumber,
            cert.IssuedAt,
            cert.StudentId,
            cert.Student?.Name ?? "Unknown Student",
            cert.CourseId,
            cert.Course?.Title ?? "Unknown Course",
            new List<LinkDto>(),
            cert.Student?.GPA,
            enrollment?.Grade
        );
    }

    public async Task<IEnumerable<CertificateResponseDto>> GetByStudentIdAsync(int studentId)
    {
        var certs = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Where(c => c.StudentId == studentId)
            .ToListAsync();

        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        return certs.Select(cert =>
        {
            var enroll = enrollments.FirstOrDefault(e => e.CourseId == cert.CourseId);
            return new CertificateResponseDto(
                cert.Id,
                cert.SerialNumber,
                cert.IssuedAt,
                cert.StudentId,
                cert.Student?.Name ?? "Unknown Student",
                cert.CourseId,
                cert.Course?.Title ?? "Unknown Course",
                null,
                cert.Student?.GPA,
                enroll?.Grade
            );
        }).ToList();
    }

    public async Task<bool> RevokeCertificateAsync(int id)
    {
        var cert = await _context.Certificates.FindAsync(id);
        if (cert == null) return false;

        _context.Certificates.Remove(cert);
        await _context.SaveChangesAsync();
        return true;
    }
}