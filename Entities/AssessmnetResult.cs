namespace TmsApi.Entities;

public class AssessmentResult
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public decimal ScoreObtained { get; set; }
    public decimal Weight { get; set; } // share of the final grade, e.g. 0.30m for 30%
                                        // Foreign key + navigation to the owning course
    public int AssessmnetId { get; set; }
    public Assessment AssessmentId { get; set; } = null!;

    // Foreign key + navigation to the assessed student
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
}
