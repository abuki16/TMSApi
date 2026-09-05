using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Api.Controllers;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using Xunit;

namespace TmsApi.Tests;

public class GradingApprovalAndValidationTests
{
    private TmsDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TmsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new TmsDbContext(options);
    }

    [Fact]
    public async Task AssessmentResultService_ThrowsException_WhenEnrollmentNotApproved()
    {
        // Arrange
        using var context = CreateDbContext(
            nameof(AssessmentResultService_ThrowsException_WhenEnrollmentNotApproved));

        var student = new Student
        {
            Id = 101,
            Name = "Pending Student",
            RegistrationNumber = "TMS-P101"
        };

        var course = new Course
        {
            Id = 201,
            Title = "Backend Architecture",
            Code = "CRS-201",
            MaxCapacity = 30
        };

        var assessment = new Assessment
        {
            Id = 301,
            Title = "Midterm Exam",
            CourseId = 201,
            MaxScore = 30,
            Weight = 30
        };

        var enrollment = new Enrollment
        {
            Id = 401,
            StudentId = 101,
            CourseId = 201,
            Status = "Pending",
            EnrolledAt = DateTime.UtcNow
        };

        context.Students.Add(student);
        context.Courses.Add(course);
        context.Assessments.Add(assessment);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var service = new AssessmentResultService(context);
        var newResult = new AssessmentResult
        {
            AssessmentId = 301,
            StudentId = 101,
            Title = "Midterm Exam",
            ScoreObtained = 25,
            Weight = 30
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateResultAsync(newResult));

        Assert.Contains("not been approved", ex.Message);
    }

    [Fact]
    public async Task AssessmentResultService_Succeeds_WhenEnrollmentIsApproved()
    {
        // Arrange
        using var context = CreateDbContext(
            nameof(AssessmentResultService_Succeeds_WhenEnrollmentIsApproved));

        var student = new Student
        {
            Id = 102,
            Name = "Approved Student",
            RegistrationNumber = "TMS-A102"
        };

        var course = new Course
        {
            Id = 202,
            Title = "DevOps Engineering",
            Code = "CRS-202",
            MaxCapacity = 30
        };

        var assessment = new Assessment
        {
            Id = 302,
            Title = "Final Exam",
            CourseId = 202,
            MaxScore = 50,
            Weight = 50
        };

        var enrollment = new Enrollment
        {
            Id = 402,
            StudentId = 102,
            CourseId = 202,
            Status = "Approved",
            EnrolledAt = DateTime.UtcNow
        };

        context.Students.Add(student);
        context.Courses.Add(course);
        context.Assessments.Add(assessment);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var service = new AssessmentResultService(context);
        var newResult = new AssessmentResult
        {
            AssessmentId = 302,
            StudentId = 102,
            Title = "Final Exam",
            ScoreObtained = 45,
            Weight = 50
        };

        // Act
        var created = await service.CreateResultAsync(newResult);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(45, created.ScoreObtained);
    }

    [Fact]
    public async Task GradesController_SubmitGrade_ReturnsBadRequest_WhenEnrollmentNotApproved()
    {
        // Arrange
        using var context = CreateDbContext(
            nameof(GradesController_SubmitGrade_ReturnsBadRequest_WhenEnrollmentNotApproved));

        var enrollment = new Enrollment
        {
            Id = 501,
            StudentId = 103,
            CourseId = 203,
            Status = "Pending",
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var controller = new GradesController(context);
        var dto = new GradeDto
        {
            StudentId = 103,
            CourseId = 203,
            Score = 88
        };

        // Act
        var result = await controller.SubmitGrade(dto);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.50)]
    [InlineData(4.01)]
    [InlineData(5.00)]
    [InlineData(100.0)]
    public async Task GradesController_UpdateEnrollmentGrade_RejectsInvalidGradeRange(decimal invalidGrade)
    {
        // Arrange
        using var context = CreateDbContext($"GradeRange_{invalidGrade}");
        var enrollment = new Enrollment
        {
            Id = 601,
            StudentId = 104,
            CourseId = 204,
            Status = "Approved",
            Grade = 3.00m,
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var controller = new GradesController(context);
        var request = new GradesController.UpdateEnrollmentGradeRequest(invalidGrade);

        // Act
        var result = await controller.UpdateEnrollmentGrade(601, request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(2.50)]
    [InlineData(3.75)]
    [InlineData(4.00)]
    public async Task GradesController_UpdateEnrollmentGrade_AcceptsValidGradeRange(decimal validGrade)
    {
        // Arrange
        using var context = CreateDbContext($"GradeRangeValid_{validGrade}");
        var student = new Student
        {
            Id = 105,
            Name = "Valid Student",
            RegistrationNumber = "TMS-V105"
        };

        var enrollment = new Enrollment
        {
            Id = 701,
            StudentId = 105,
            CourseId = 205,
            Status = "Approved",
            Grade = 2.00m,
            EnrolledAt = DateTime.UtcNow
        };

        context.Students.Add(student);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var controller = new GradesController(context);
        var request = new GradesController.UpdateEnrollmentGradeRequest(validGrade);

        // Act
        var result = await controller.UpdateEnrollmentGrade(701, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal(validGrade, enrollment.Grade);
    }

    [Fact]
    public async Task CertificateService_IssueCertificate_ThrowsOutOfRange_WhenGradeExceedsFour()
    {
        // Arrange
        using var context = CreateDbContext(
            nameof(CertificateService_IssueCertificate_ThrowsOutOfRange_WhenGradeExceedsFour));

        var student = new Student
        {
            Id = 106,
            Name = "Cert Student",
            RegistrationNumber = "TMS-C106"
        };

        var course = new Course
        {
            Id = 206,
            Title = "Enterprise Cloud",
            Code = "CRS-206",
            MaxCapacity = 30
        };

        var enrollment = new Enrollment
        {
            Id = 801,
            StudentId = 106,
            CourseId = 206,
            Status = "Approved",
            Grade = 3.50m,
            EnrolledAt = DateTime.UtcNow
        };

        context.Students.Add(student);
        context.Courses.Add(course);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var service = new CertificateService(context);
        var request = new IssueCertificateRequest(
            StudentId: 106,
            CourseId: 206,
            SerialNumber: "CERT-2026-TEST",
            Grade: 4.50m
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IssueCertificateAsync(request));
    }
}
