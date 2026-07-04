namespace TmsApi.Dtos;

// Used for incoming updates via PUT
public record UpdateCourseRequest(string Code, string Title, int MaxCapacity);
