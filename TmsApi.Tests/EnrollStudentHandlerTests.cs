using NSubstitute;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Tests;

public class EnrollStudentHandlerTests
{
    [Fact]
    public async Task Handle_WhenAlreadyEnrolled_ReturnsDuplicateError()
    {
        // Arrange
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();

        enrollmentService
            .ExistsAsync(99, "CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var courseDto = new CourseResponseDto(
            Id: 1,
            Code: "CS-401",
            Title: "Advanced Web Dev",
            MaxCapacity: 30,
            EnrollmentCount: 0,
            Enrollments: new List<EnrollmentItemDto>()
        );

        courseService
            .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CourseResponseDto?>(courseDto));

        var handler = new EnrollStudentHandler(enrollmentService, courseService);
        var command = new EnrollStudentCommand(StudentId: 99, CourseCode: "CS-401");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("already_enrolled", result.Error.Code);
        Assert.Equal(EnrollmentError.AlreadyEnrolled(99, "CS-401"), result.Error);

        await enrollmentService
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCourseFull_ReturnsCapacityError()
    {
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();

        // Populate Enrollments collection to match MaxCapacity (35) using the 2-argument constructor
        var dummyEnrollments = Enumerable.Range(1, 35)
            .Select(i => new EnrollmentItemDto(i, StudentId: i))
            .ToList();

        var courseDto = new CourseResponseDto(
            Id: 1,
            Code: "CS-401",
            Title: "Advanced Web Dev",
            MaxCapacity: 35,
            EnrollmentCount: 35,
            Enrollments: dummyEnrollments
        );

        courseService
            .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CourseResponseDto?>(courseDto));

        var handler = new EnrollStudentHandler(enrollmentService, courseService);
        var command = new EnrollStudentCommand(StudentId: 100, CourseCode: "CS-401");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("course_full", result.Error.Code);
        Assert.Equal(EnrollmentError.CourseFull("Advanced Web Dev", 35), result.Error);

        await enrollmentService
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulPath_AddsEnrollmentOnce()
    {
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();

        // Populate Enrollments collection with fewer items than MaxCapacity (20 out of 35)
        var dummyEnrollments = Enumerable.Range(1, 20)
            .Select(i => new EnrollmentItemDto(i, StudentId: i))
            .ToList();

        var courseDto = new CourseResponseDto(
            Id: 1,
            Code: "CS-401",
            Title: "Advanced Web Dev",
            MaxCapacity: 35,
            EnrollmentCount: 20,
            Enrollments: dummyEnrollments
        );

        courseService
            .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CourseResponseDto?>(courseDto));

        enrollmentService
            .ExistsAsync(100, "CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var handler = new EnrollStudentHandler(enrollmentService, courseService);
        var command = new EnrollStudentCommand(StudentId: 100, CourseCode: "CS-401");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.StudentId);
        Assert.Equal("CS-401", result.Value.CourseCode);

        await enrollmentService
            .Received(1)
            .AddAsync(
                Arg.Is<Enrollment>(e => e.StudentId == 100 && e.CourseId == 1),
                Arg.Any<CancellationToken>());
    }
}