namespace TmsApi.Application.Hubs;
public interface ITmsHubClient
{
Task ReceiveTranscriptReady(string reportId, string downloadUrl);
Task ReceiveCourseUpdate(string courseCode, string message);
Task ReceiveGradePosted(string courseCode, int studentId, decimal grade);

// New: broadcast enrollment status changes to all connected clients
Task ReceiveEnrollmentStatusUpdated(string enrollmentId, string status);
}