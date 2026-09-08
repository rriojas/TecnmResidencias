namespace TecNM.Residency.Common.EmailVerification;

public interface IEmailVerificationService
{
    /// <summary>
    /// Validates email syntax, removes spaces, checks allowed domains from configuration,
    /// and performs live DNS MX record lookup to ensure the email host exists and can receive mail.
    /// </summary>
    /// <param name="email">The email string to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with clean sanitized email on success, or Failure with explanation.</returns>
    Task<Result<string>> ValidateAndVerifyEmailAsync(string? email, CancellationToken cancellationToken = default);
}
