using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Gmail;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Shared.Config;
using Xunit;

namespace NextStep.Tests;

/// <summary>Answers every HTTP call with the queued responses and records the requested URLs.</summary>
internal sealed class QueueHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();
    public List<string> Requests { get; } = [];

    public QueueHttpHandler Respond(HttpStatusCode status, string body)
    {
        _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        return this;
    }

    public QueueHttpHandler Throw()
    {
        _responses.Enqueue(() => throw new HttpRequestException("network down"));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!.ToString());
        return Task.FromResult(_responses.Dequeue()());
    }
}

public class GmailTokenProviderTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private readonly QueueHttpHandler _http = new();
    private readonly Mock<IUserEmailConnectionRepository> _connections = new();
    private readonly Mock<IUserOAuthCredentialRepository> _credentials = new();
    private UserEmailConnection? _stored;

    private GmailTokenProvider Provider()
    {
        _connections.Setup(r => r.GetByUserAndProviderAsync(_userId, "Gmail", It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored);
        _connections.Setup(r => r.UpsertAsync(It.IsAny<UserEmailConnection>(), It.IsAny<CancellationToken>()))
            .Callback<UserEmailConnection, CancellationToken>((c, _) => _stored = c).Returns(Task.CompletedTask);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_http, disposeHandler: false));
        var options = Options.Create(new GoogleOAuthOptions { ClientId = "id.apps.googleusercontent.com", ClientSecret = "secret", RedirectUri = "http://localhost/cb" });
        return new GmailTokenProvider(_connections.Object, _credentials.Object, factory.Object, _protection, options, NullLogger<GmailTokenProvider>.Instance);
    }

    private void Connect(DateTime accessExpiresAt, string refreshToken = "refresh-1", string? reconnectReason = null)
    {
        var protector = _protection.CreateProtector("GmailOAuthTokens");
        _stored = new UserEmailConnection
        {
            UserId = _userId,
            EmailAddress = "me@gmail.com",
            AccessTokenEncrypted = protector.Protect("access-1"),
            RefreshTokenEncrypted = protector.Protect(refreshToken),
            AccessTokenExpiresAtUtc = accessExpiresAt,
            ReconnectReason = reconnectReason,
        };
    }

    [Fact]
    public async Task Valid_access_token_is_used_without_calling_Google()
    {
        Connect(DateTime.UtcNow.AddMinutes(30));
        var access = await Provider().GetAccessAsync(_userId);
        access.AccessToken.Should().Be("access-1");
        _http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Expired_access_token_is_refreshed_and_saved()
    {
        Connect(DateTime.UtcNow.AddMinutes(-5), reconnectReason: GmailMessages.Revoked);
        _http.Respond(HttpStatusCode.OK, """{"access_token":"access-2","expires_in":3599}""");

        var access = await Provider().GetAccessAsync(_userId);

        access.AccessToken.Should().Be("access-2");
        _stored!.AccessTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(50));
        _stored.ReconnectReason.Should().BeNull("a working refresh clears an old problem");
        _protection.CreateProtector("GmailOAuthTokens").Unprotect(_stored.RefreshTokenEncrypted).Should().Be("refresh-1");
    }

    [Fact]
    public async Task Revoked_refresh_token_asks_the_user_to_reconnect()
    {
        Connect(DateTime.UtcNow.AddMinutes(-5));
        _http.Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Token has been expired or revoked."}""");

        var access = await Provider().GetAccessAsync(_userId);

        access.Success.Should().BeFalse();
        access.Failure.Should().Be(GmailFailure.Revoked);
        access.NeedsUserAction.Should().BeTrue();
        access.UserMessage.Should().Be(GmailMessages.Revoked).And.NotContain("invalid_grant");
        _stored!.ReconnectReason.Should().Be(GmailMessages.Revoked);
    }

    [Fact]
    public async Task Network_error_is_temporary_and_not_recorded()
    {
        Connect(DateTime.UtcNow.AddMinutes(-5));
        _http.Throw();

        var access = await Provider().GetAccessAsync(_userId);

        access.Failure.Should().Be(GmailFailure.Transient);
        access.NeedsUserAction.Should().BeFalse();
        access.UserMessage.Should().NotContain("network down");
        _stored!.ReconnectReason.Should().BeNull();
    }

    [Fact]
    public async Task Unreadable_tokens_ask_the_user_to_reconnect()
    {
        Connect(DateTime.UtcNow.AddMinutes(30));
        _stored!.AccessTokenEncrypted = "not-a-protected-value";

        var access = await Provider().GetAccessAsync(_userId);

        access.Failure.Should().Be(GmailFailure.Unreadable);
        _stored.ReconnectReason.Should().Be(GmailMessages.Unreadable);
    }

    [Fact]
    public async Task Background_work_skips_a_connection_known_to_need_reconnecting()
    {
        Connect(DateTime.UtcNow.AddMinutes(-5), reconnectReason: GmailMessages.Revoked);

        var access = await Provider().GetAccessAsync(_userId, skipIfReconnectRequired: true);

        access.Success.Should().BeFalse();
        _http.Requests.Should().BeEmpty("Google must not be called again for a revoked connection");
    }

    [Fact]
    public async Task No_connection_is_reported_as_not_connected()
    {
        _stored = null;
        var access = await Provider().GetAccessAsync(_userId);
        access.Failure.Should().Be(GmailFailure.NotConnected);
        access.UserMessage.Should().Be(GmailMessages.NotConnected);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""", GmailFailure.Revoked)]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""", GmailFailure.ClientRejected)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"unauthorized_client"}""", GmailFailure.ClientRejected)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_scope"}""", GmailFailure.MissingPermission)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "<html>down</html>", GmailFailure.Transient)]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", GmailFailure.Transient)]
    public void Token_endpoint_errors_are_classified(HttpStatusCode status, string body, GmailFailure expected)
    {
        GmailTokenProvider.ClassifyTokenError(status, body).Should().Be(expected);
    }

    [Theory]
    [InlineData(401, "{}", GmailFailure.Revoked)]
    [InlineData(403, """{"error":{"errors":[{"reason":"insufficientPermissions"}]}}""", GmailFailure.MissingPermission)]
    [InlineData(403, """{"error":{"errors":[{"reason":"userRateLimitExceeded"}]}}""", GmailFailure.Transient)]
    [InlineData(429, "{}", GmailFailure.Transient)]
    [InlineData(503, "oops", GmailFailure.Transient)]
    [InlineData(400, """{"error":{"message":"Invalid To header"}}""", GmailFailure.None)]
    public void Gmail_api_errors_are_classified_without_leaking_the_body(int status, string body, GmailFailure expected)
    {
        var (failure, message) = GmailApiErrors.Describe((HttpStatusCode)status, body);
        failure.Should().Be(expected);
        message.Should().NotContain("{").And.NotContain("Invalid To header");
    }
}

public class GmailSenderValidationTests
{
    [Theory]
    [InlineData("recruiter@acme.com\r\nBcc: attacker@evil.com")]
    [InlineData("a@b.com, c@d.com")]
    [InlineData("Name <a@b.com>")]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Invalid_or_injected_recipients_are_refused_before_any_Gmail_call(string recipient)
    {
        var tokens = new Mock<IGmailTokenProvider>(MockBehavior.Strict);
        var sender = new GmailEmailSenderService(tokens.Object, Mock.Of<IHttpClientFactory>(), NullLogger<GmailEmailSenderService>.Instance);

        var result = await sender.SendAsync(Guid.NewGuid(), recipient, "Subject", "Body");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The recipient email address is not valid.");
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reconnect_needed_is_reported_to_the_caller()
    {
        var tokens = new Mock<IGmailTokenProvider>();
        tokens.Setup(t => t.GetAccessAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new GmailAccess(new UserEmailConnection(), null, GmailFailure.Revoked, GmailMessages.Revoked));
        var sender = new GmailEmailSenderService(tokens.Object, Mock.Of<IHttpClientFactory>(), NullLogger<GmailEmailSenderService>.Instance);

        var result = await sender.SendAsync(Guid.NewGuid(), "recruiter@acme.com", "Subject", "Body");

        result.Success.Should().BeFalse();
        result.NeedsReconnect.Should().BeTrue();
        result.ErrorMessage.Should().Be(GmailMessages.Revoked);
    }
}
