using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace NextStep.Shared.ErrorHandling;

/// <summary>
/// Produces the single, uniform error contract used across all controllers:
///   { "error": "...", "type": "...", "traceId": "..."?, "details": {...}? }
/// This mirrors the shape emitted by <see cref="GlobalExceptionHandler"/> so the
/// frontend can extract a user-friendly message from every non-success response.
/// </summary>
public static class ApiResult
{
    public static ObjectResult Error(
        string message,
        HttpStatusCode status = HttpStatusCode.InternalServerError,
        string? type = null,
        Dictionary<string, string[]>? details = null)
        => new(new ApiErrorEnvelope
        {
            Error = message,
            Type = type,
            Details = details,
        })
        {
            StatusCode = (int)status,
        };

    public static ObjectResult BadRequest(string message, string? type = "ValidationError", Dictionary<string, string[]>? details = null)
        => Error(message, HttpStatusCode.BadRequest, type, details);

    public static ObjectResult NotFound(string message = "Ressource introuvable.", string? type = "NotFound")
        => Error(message, HttpStatusCode.NotFound, type);

    public static ObjectResult Unauthorized(string message = "Accès refusé.", string? type = "Unauthorized")
        => Error(message, HttpStatusCode.Unauthorized, type);

    public static ObjectResult Forbidden(string message = "Accès refusé.", string? type = "Forbidden")
        => Error(message, HttpStatusCode.Forbidden, type);

    public static ObjectResult Conflict(string message, string? type = "Conflict")
        => Error(message, HttpStatusCode.Conflict, type);
}