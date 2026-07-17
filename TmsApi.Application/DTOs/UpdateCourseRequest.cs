namespace TmsApi.Application.DTOs;

// Used for incoming updates via PUT
public record UpdateCourseRequest(string Code, string Title, int MaxCapacity);
