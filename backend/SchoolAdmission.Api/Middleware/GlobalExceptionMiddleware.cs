using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Dtos;

namespace SchoolAdmission.Api.Middleware;

/// <summary>
/// Global exception-handling middleware that converts invalid requests and errors into clean JSON responses.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (statusCode, message) = MapException(ex);
            _logger.LogWarning(ex, "Request failed with mapped status {StatusCode}: {Message}", statusCode, message);

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(message, statusCode));
        }
    }

    private static (int StatusCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            ApiException apiEx => (apiEx.StatusCode, apiEx.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Requested resource was not found."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed HTTP request."),
            JsonException => (StatusCodes.Status400BadRequest, "Invalid JSON payload."),
            ValidationException validationEx => (StatusCodes.Status400BadRequest, validationEx.Message),
            ArgumentException argEx => (StatusCodes.Status400BadRequest, argEx.Message),
            FormatException => (StatusCodes.Status400BadRequest, "Invalid parameter format."),
            DbUpdateException dbEx => MapDbUpdateException(dbEx),
            PostgresException pgEx => MapPostgresException(pgEx),
            NpgsqlException or SocketException or TimeoutException =>
                (StatusCodes.Status503ServiceUnavailable, "Database service is temporarily unavailable."),
            _ => (StatusCodes.Status400BadRequest, "Request could not be processed.")
        };
    }

    private static (int StatusCode, string Message) MapDbUpdateException(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException pgEx)
        {
            return MapPostgresException(pgEx);
        }

        return (StatusCodes.Status400BadRequest, "Database update rejected due to invalid data.");
    }

    private static (int StatusCode, string Message) MapPostgresException(PostgresException exception) =>
        exception.SqlState switch
        {
            PostgresErrorCodes.ForeignKeyViolation => (StatusCodes.Status404NotFound, "Referenced resource was not found."),
            PostgresErrorCodes.UniqueViolation => (StatusCodes.Status409Conflict, "Conflicting resource already exists."),
            PostgresErrorCodes.InvalidTextRepresentation or PostgresErrorCodes.CheckViolation =>
                (StatusCodes.Status400BadRequest, "Invalid data or constraint violation."),
            _ => (StatusCodes.Status400BadRequest, "Database request could not be completed.")
        };
}

