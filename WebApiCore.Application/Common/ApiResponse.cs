namespace WebApiCore.Application.Common;

public class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data, string? message = null, int statusCode = 200)
        => new()
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Failure<T>(int statusCode, string message, IDictionary<string, string[]>? errors = null, string? traceId = null)
        => new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message,
            Errors = errors,
            TraceId = traceId
        };
}