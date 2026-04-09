namespace PackTracker.Application.Common;

public class OperationResult<T> : Result
{
    public T? Data { get; init; }

    public static OperationResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static new OperationResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}