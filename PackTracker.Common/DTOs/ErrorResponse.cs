namespace PackTracker.Common.DTOs;

public class ErrorResponse
{
    public string Message { get; set; } = "An unexpected error occurred";
    public string TraceId { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Optional dictionary for extra error info (e.g., field-level validation)
    public IDictionary<string, string[]>? Errors { get; set; }
}