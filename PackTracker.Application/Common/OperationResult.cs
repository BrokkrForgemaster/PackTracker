namespace PackTracker.Application.Common;

/// <summary name="OperationResult">
/// Represents the result of an operation, including success status, an optional message, and optional data.
/// </summary>
/// <typeparam name="T">
/// The type of the data returned by the operation. This can be any type, such as a DTO, a domain model, or a primitive type.
/// </typeparam>
public class OperationResult<T> : Result
{
    public T? Data { get; init; }

    public static OperationResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static new OperationResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}