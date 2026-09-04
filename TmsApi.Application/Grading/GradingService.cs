namespace TmsApi.Application.Grading;

public class GradingService
{
    public const decimal DistinctionThreshold = 70m;
    public const decimal PassThreshold = 50m;

    // Pure mapping: one score against one maximum.
    // Uses M5's Assessment.MaxScore and the decimal part of Enrollment.Grade.
    public GradeLevel CalculateLetterGrade(decimal score, decimal maxScore)
    {
        if (maxScore <= 0m || score < 0m || score > maxScore)
            return GradeLevel.Invalid;

        var pct = score / maxScore * 100m;

        return pct >= DistinctionThreshold ? GradeLevel.Distinction
             : pct >= PassThreshold ? GradeLevel.Pass
             : GradeLevel.Fail;
    }

    // Single-decimal path: maps an Enrollment.Grade percentage to a GradeLevel.
    // Enrollment.Grade is nullable per the M5 entity; null => Invalid.
    public GradeLevel CalculateFromEnrollmentGrade(decimal? enrollmentGradePercent)
    {
        if (enrollmentGradePercent is null) 
            return GradeLevel.Invalid;

        return CalculateLetterGrade(enrollmentGradePercent.Value, maxScore: 100m);
    }

    public static string ToInstitutionalLetterGrade(decimal totalScore)
    {
        if (totalScore >= 90m) return "A+";
        if (totalScore >= 85m) return "A";
        if (totalScore >= 80m) return "A-";
        if (totalScore >= 75m) return "B+";
        if (totalScore >= 70m) return "B";
        if (totalScore >= 60m) return "B-";
        if (totalScore >= 55m) return "C+";
        if (totalScore >= 50m) return "C";
        return "Fail";
    }

    public static decimal ToInstitutionalGradePoint(decimal totalScore)
    {
        if (totalScore >= 85m) return 4.00m;
        if (totalScore >= 80m) return 3.75m;
        if (totalScore >= 75m) return 3.50m;
        if (totalScore >= 70m) return 3.00m;
        if (totalScore >= 60m) return 2.75m;
        if (totalScore >= 55m) return 2.50m;
        if (totalScore >= 50m) return 2.00m;
        return 0.00m;
    }
}