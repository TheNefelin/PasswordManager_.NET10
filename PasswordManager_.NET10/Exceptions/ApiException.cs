namespace PasswordManager_.NET10.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }

    public ApiException(string message, int statusCode = 0, string? errorMessage = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
    }
}