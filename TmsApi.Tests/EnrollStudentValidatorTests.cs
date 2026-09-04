using FluentValidation.TestHelper;
using TmsApi.Application.Enrollments.Commands;

namespace TmsApi.Tests;

public class EnrollStudentValidatorTests
{
    private readonly EnrollStudentValidator _validator = new();

    [Theory]
    [InlineData("AI-101")]
    [InlineData("UX-101")]
    [InlineData("CS-101")]
    [InlineData("CSE-101")]
    [InlineData("DAT-201")]
    [InlineData("MATH-101")]
    [InlineData("PHYS-301")]
    public void Validate_ValidCourseCodes_PassesValidation(string courseCode)
    {
        var command = new EnrollStudentCommand(StudentId: 1, CourseCode: courseCode);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.CourseCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A-101")]
    [InlineData("TOOLONG-101")]
    [InlineData("123-456")]
    [InlineData("ai-101")]
    [InlineData("AI-12")]
    [InlineData("CSE101")]
    public void Validate_InvalidCourseCodes_FailsValidation(string courseCode)
    {
        var command = new EnrollStudentCommand(StudentId: 1, CourseCode: courseCode);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CourseCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidStudentId_FailsValidation(int studentId)
    {
        var command = new EnrollStudentCommand(StudentId: studentId, CourseCode: "AI-101");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StudentId);
    }
}
