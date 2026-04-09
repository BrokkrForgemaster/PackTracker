namespace PackTracker.Application.Common;

/// <summary>
/// Represents the result of an operation, including success state and optional message.
/// </summary>
public class Result
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets an optional message describing the result.
    /// </summary>
    public string? Message { get; init; }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Ok(string? message = null) =>
        new()
        {
            Success = true,
            Message = message
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result Fail(string message) =>
        new()
        {
            Success = false,
            Message = message
        };

    #endregion
    
    
}

