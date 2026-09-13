namespace KeyFactorDashboard.Services;

public sealed class PythonApiException : Exception
{
    public PythonApiException(int statusCode, string? errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode ?? "pythonError";
    }

    public int StatusCode { get; }
    public string ErrorCode { get; }
}
