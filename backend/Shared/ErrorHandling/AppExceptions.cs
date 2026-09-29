using System.Net;

namespace NextStep.Shared.ErrorHandling;

/// <summary>
/// An expected business failure, thrown by services and turned by <see cref="GlobalExceptionHandler"/>
/// into the standard error contract <c>{ "error": message, "type": ... }</c> with <see cref="Status"/>.
/// Controllers therefore do not need try/catch blocks to translate errors into responses.
/// </summary>
public abstract class AppException(string message, HttpStatusCode status, string type, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode Status { get; } = status;
    public string Type { get; } = type;
}

/// <summary>404 — the resource does not exist (or is not visible to the caller).</summary>
public sealed class NotFoundException(string message = "Resource not found.")
    : AppException(message, HttpStatusCode.NotFound, "NotFound");

/// <summary>403 — the caller is authenticated but may not access the resource.</summary>
public sealed class ForbiddenException(string message = "Access denied.")
    : AppException(message, HttpStatusCode.Forbidden, "Forbidden");

/// <summary>401 — the caller could not be identified.</summary>
public sealed class UnauthorizedException(string message = "Unauthorized access.")
    : AppException(message, HttpStatusCode.Unauthorized, "Unauthorized");

/// <summary>400 — the request is invalid.</summary>
public sealed class BadRequestException(string message)
    : AppException(message, HttpStatusCode.BadRequest, "ValidationError");

/// <summary>409 — the request conflicts with the current state (e.g. draft already sent).</summary>
public sealed class ConflictException(string message, Exception? inner = null)
    : AppException(message, HttpStatusCode.Conflict, "InvalidOperation", inner);

/// <summary>409 — the user must (re)connect their email account in settings before this can work.</summary>
public sealed class EmailReconnectRequiredException(string message)
    : AppException(message, HttpStatusCode.Conflict, "EmailReconnectRequired");

/// <summary>502 — a dependency (Python agents, storage, Gmail) failed.</summary>
public sealed class UpstreamServiceException(string message, Exception? inner = null)
    : AppException(message, HttpStatusCode.BadGateway, "UpstreamError", inner);

/// <summary>500 — the operation failed; the message is safe to show to the user.</summary>
public sealed class OperationFailedException(string message, Exception? inner = null)
    : AppException(message, HttpStatusCode.InternalServerError, "InternalServerError", inner);
