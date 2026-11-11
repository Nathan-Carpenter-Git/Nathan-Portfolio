namespace NathanPortfolio.CustomServices
{
    public interface ITurnstileValidator
    {
        /// <summary>
        /// Verifies a Cloudflare Turnstile token with Cloudflare's siteverify endpoint.
        /// Returns false for a missing, invalid, expired or reused token, and also when
        /// verification itself fails (fail closed), so callers can simply reject on false.
        /// </summary>
        Task<bool> ValidateAsync(string? token, CancellationToken cancellationToken = default);
    }
}
