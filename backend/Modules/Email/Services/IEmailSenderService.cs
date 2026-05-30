namespace NextStep.Modules.Email.Services;

/// <summary>
/// Abstraction for sending emails through an email provider.
/// The implementation is responsible for auth, token refresh, and provider API calls.
/// </summary>
public interface IEmailSenderService
{
    /// <summary>
    /// Sends an email on behalf of the given local user through the connected provider.
    /// </summary>
    /// <param name="localUserId">The local DB user ID (utilisateur.id_utilisateur).</param>
    /// <param name="recipientEmail">The recruiter/company email recipient.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body (plain text).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// SendEmailResult with Success=true and ProviderMessageId on success,
    /// or Success=false and ErrorMessage on failure.
    /// Never throws for provider-level failures — wraps errors in the result.
    /// </returns>
    Task<SendEmailResult> SendAsync(
        Guid localUserId,
        string recipientEmail,
        string subject,
        string body,
        string? attachmentName = null,
        byte[]? attachmentBytes = null,
        string? attachmentContentType = null,
        CancellationToken cancellationToken = default);
}
