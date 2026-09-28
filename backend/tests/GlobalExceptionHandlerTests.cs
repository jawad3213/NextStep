using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NextStep.Shared.ErrorHandling;
using Xunit;

namespace NextStep.Tests;

/// <summary>Services throw application exceptions; the handler renders the standard error contract.</summary>
public class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData("NotFound", 404, "NotFound")]
    [InlineData("Forbidden", 403, "Forbidden")]
    [InlineData("Unauthorized", 401, "Unauthorized")]
    [InlineData("BadRequest", 400, "ValidationError")]
    [InlineData("Conflict", 409, "InvalidOperation")]
    [InlineData("Upstream", 502, "UpstreamError")]
    [InlineData("OperationFailed", 500, "InternalServerError")]
    public async Task App_exceptions_become_their_status_with_the_service_message(string kind, int status, string type)
    {
        const string message = "Candidature introuvable.";
        AppException exception = kind switch
        {
            "NotFound" => new NotFoundException(message),
            "Forbidden" => new ForbiddenException(message),
            "Unauthorized" => new UnauthorizedException(message),
            "BadRequest" => new BadRequestException(message),
            "Conflict" => new ConflictException(message),
            "Upstream" => new UpstreamServiceException(message),
            _ => new OperationFailedException(message),
        };
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(status);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("error").GetString().Should().Be(message);
        body.RootElement.GetProperty("type").GetString().Should().Be(type);
    }
}
