namespace SchoolAdmission.Api.Middleware;

/// <summary>
/// Exception representing a controlled HTTP API error response.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

