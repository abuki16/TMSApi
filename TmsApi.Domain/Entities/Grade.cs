using System;
namespace TmsApi.Domain.Entities;

public class Grade
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int Score { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}