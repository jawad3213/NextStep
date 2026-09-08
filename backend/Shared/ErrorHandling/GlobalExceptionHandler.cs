using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NextStep.Shared.ErrorHandling;

/// <summary>
/// Centralised exception handler — every unhandled exception that escapes a
/// controller/filter is caught here and converted into a uniform JSON response.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, error, type) = exception switch
        {
            KeyNotFoundException
                => (HttpStatusCode.NotFound, "Resource not found", "NotFound"),

            ArgumentException argex
                => (HttpStatusCode.BadRequest, argex.Message, "ValidationError"),

            UnauthorizedAccessException
                => (HttpStatusCode.Unauthorized, "Access denied", "Unauthorized"),

            InvalidOperationException opex
                => (HttpStatusCode.Conflict, opex.Message, "InvalidOperation"),

            OperationCanceledException
                => (HttpStatusCode.RequestTimeout, "Request was cancelled or timed out", "RequestCancelled"),

            System.Data.DataException or Microsoft.EntityFrameworkCore.DbUpdateException
                => (HttpStatusCode.Conflict, "A data integrity error occurred", "DataError"),

            System.Net.Http.HttpRequestException httpEx
                => (HttpStatusCode.BadGateway, "An upstream service call failed", "UpstreamError"),

            NotImplementedException
                => (HttpStatusCode.NotImplemented, "This feature is not yet implemented", "NotImplemented"),

            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred", "InternalServerError"),
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        _logger.LogError(exception,
            "Unhandled exception {ExceptionType} → {StatusCode} (trace={TraceId})",
            exception.GetType().Name, (int)statusCode, traceId);

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        var envelope = new ApiErrorEnvelope
        {
            Error = error,
            Type = type,
            TraceId = traceId,
            Details = GetDetailsIfValidation(exception),
        };

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(envelope, JsonOpts), cancellationToken);

        return true; // handled
    }

    private static Dictionary<string, string[]>? GetDetailsIfValidation(Exception ex)
    {
        if (ex is ArgumentException aex && !string.IsNullOrWhiteSpace(aex.ParamName))
            return new Dictionary<string, string[]>
            {
                [aex.ParamName] = new[] { aex.Message }
            };
        return null;
    }
}

public sealed class ApiErrorEnvelope
{
    public required string Error { get; set; }
    public string? Type { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string[]>? Details { get; set; }
}
