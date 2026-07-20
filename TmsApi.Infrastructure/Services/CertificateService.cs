
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
        // Return null (or throw a custom exception) to prevent duplicate certificate creation
        return null; 
    }

    // 2. Your existing validation checks (e.g., verifying if Student and Course exist)...
    var studentExists = await _context.Students.AnyAsync(s => s.Id == request.StudentId);
    var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId);
    
    if (!studentExists || !courseExists)
    {
        return null;
    }

    // 3. Create and save the certificate if all checks pass
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

        return new CertificateResponseDto(
            cert.Id,
            cert.SerialNumber,
            cert.IssuedAt,
            cert.StudentId,
            cert.Student?.Name ?? "Unknown Student",// Adjust property name to fit your Student model
            cert.CourseId,
            cert.Course?.Title ?? "Unknown Course",
            new List<LinkDto>() // Populated contextually by the controller
        );
    }

    public async Task<IEnumerable<CertificateResponseDto>> GetByStudentIdAsync(int studentId)
    {
        return await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Where(c => c.StudentId == studentId)
            .Select(cert => new CertificateResponseDto(
                cert.Id,
                cert.SerialNumber,
                cert.IssuedAt,
                cert.StudentId,
               cert.Student.Name,
                cert.CourseId,
                cert.Course.Title
            ))
            .ToListAsync();
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