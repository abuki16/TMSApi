namespace TmsApi.Domain.Entities;

public class AssessmentResult
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public decimal ScoreObtained { get; set; }
    public decimal Weight { get; set; }

    // Foreign Key (stores the integer ID)
    public int AssessmentId { get; set; }
    
    // Navigation Property (stores the related Assessment class object)
    public Assessment Assessment { get; set; } = null!;

    // Foreign Key
    public int StudentId { get; set; }
    
    // Navigation Property
    public Student Student { get; set; } = null!;
}