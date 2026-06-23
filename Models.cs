
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Logging;
// //using TmsApi.Controllers;

// namespace TmsApi.Controllers;


// // Exercise 3 — Record
// public record EnrollmentRecord(
//     string StudentId,
//     string CourseCode,
//     DateTime EnrolledAt
// );


// // Exercise 3 Part 2 — Course

// public class Course
// {
//     public required string Code { get; init; }

//     public required string Title
//     {
//         get;
//         set => field = !string.IsNullOrWhiteSpace(value)
//             ? value
//             : throw new ArgumentException(
//                 "Title cannot be empty or whitespace.",
//                 nameof(value)
//             );
//     }

//     public int Capacity
//     {
//         get;
//         set => field = value > 0
//             ? value
//             : throw new ArgumentOutOfRangeException(
//                 nameof(value),
//                 "System constraint: Capacity must be greater than zero."
//             );
//     }

//     public int EnrolledCount { get; set; }
// }


// // Exercise 3 Part 3 — Student

// public class Student
// {
//     public required string Id { get; init; }

//     public required string Name
//     {
//         get;
//         set => field = !string.IsNullOrWhiteSpace(value)
//             ? value
//             : throw new ArgumentException(
//                 "Name cannot be empty or whitespace.",
//                 nameof(value)
//             );
//     }

//     public int Age
//     {
//         get;
//         set => field = value is >= 16 and <= 100
//             ? value
//             : throw new ArgumentOutOfRangeException(
//                 nameof(value),
//                 "Age must be between 16 and 100."
//             );
//     }

//     public decimal GPA
//     {
//         get;
//         set => field = value is >= 0.0m and <= 4.0m
//             ? value
//             : throw new ArgumentOutOfRangeException(
//                 nameof(value),
//                 "GPA must be between 0.0 and 4.0."
//             );
//     }
// }


// // Exercise 3B — Interface Contract

// public interface IGradable
// {
//     string Title { get; }

//     decimal CalculateGrade();
// }


// // Quiz

// public class Quiz : IGradable
// {
//     public required string Title { get; init; }

//     public required int CorrectAnswers { get; init; }

//     public required int TotalQuestions { get; init; }

//     public decimal CalculateGrade()
//     {
//         if (TotalQuestions == 0)
//             return 0m;

//         return (decimal)CorrectAnswers / TotalQuestions * 100m;
//     }
// }


// // Lab Assignment

// public class LabAssignment : IGradable
// {
//     public required string Title { get; init; }

//     public required decimal FunctionalityScore { get; init; }

//     public required decimal CodeQualityScore { get; init; }

//     public decimal CalculateGrade()
//     {
//         return (FunctionalityScore * 0.7m)
//              + (CodeQualityScore * 0.3m);
//     }
// }

// public class TmsDatabaseException : Exception
// {
//     public string Operation { get; }

//     public TmsDatabaseException(
//         string operation,
//         string message
//     ) : base(message)
//     {
//         Operation = operation;
//     }

//     public TmsDatabaseException(
//         string operation,
//         string message,
//         Exception innerException
//     ) : base(message, innerException)
//     {
//         Operation = operation;
//     }
// }


// public class CapacityReachedException : InvalidOperationException
// {
//     public string CourseCode { get; }

//     public CapacityReachedException(string courseCode)
//         : base($"Course {courseCode} has reached maximum capacity.")
//     {
//         CourseCode = courseCode;
//     }

//     public CapacityReachedException(
//         string courseCode,
//         Exception innerException
//     ) : base(
//         $"Course {courseCode} has reached maximum capacity.",
//         innerException
//     )
//     {
//         CourseCode = courseCode;
//     }
// }