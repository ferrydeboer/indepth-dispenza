namespace InDepthDispenza.Functions.Interfaces;

public class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }

    public static ServiceResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ServiceResult<T> Failure(string errorMessage, Exception? exception = null) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}

public class ServiceResult : ServiceResult<object>
{
    public static ServiceResult Success() => new()
    {
        IsSuccess = true
    };

    public static new ServiceResult Failure(string errorMessage, Exception? exception = null) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}