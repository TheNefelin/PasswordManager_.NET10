namespace PasswordManager_.NET10.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public string? TraceId { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public ApiException(
        string message,
        int statusCode = 0,
        string? errorMessage = null,
        string? traceId = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        TraceId = traceId;
        ValidationErrors = validationErrors;
    }
}
