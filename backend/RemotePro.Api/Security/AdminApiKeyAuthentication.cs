using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RemotePro.Api.Security;

public static class AdminApiKeyDefaults
{
    public const string AuthenticationScheme = "AdminApiKey";

    // Admin client yeh header bhejta hai.
    public const string HeaderName = "X-Admin-Key";

    // Key kahan se padhi jati hai: user-secrets (development) ya
    // environment variable Admin__ApiKey (production). Source code mein kabhi nahi.
    public const string ConfigurationKey = "Admin:ApiKey";
}

internal sealed class AdminApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // Generated 32-byte Base64 key sirf 44 chars ki hoti hai; isse bada header galat hai.
    private const int MaxKeyLength = 512;

    private readonly IConfiguration _configuration;

    public AdminApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Header nahi hai: koi credential hi nahi. Heartbeat jaise requests par
        // yahan se kuch kaam nahi hota. Protected endpoint par yeh 401 banega.
        if (!Request.Headers.TryGetValue(AdminApiKeyDefaults.HeaderName, out var headerValues) ||
            headerValues.Count != 1 ||
            string.IsNullOrEmpty(headerValues[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValues[0]!;

        // Bahut lamba header hash karne se pehle hi reject.
        if (providedKey.Length > MaxKeyLength)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var expectedKey = _configuration[AdminApiKeyDefaults.ConfigurationKey];

        // Server par key configure nahi hai: fail closed (koi bhi key accept nahi hogi).
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Admin API key is not configured on the server."));
        }

        // Dono ka SHA-256 le kar fixed-time compare: timing se key ka andaza na lage,
        // aur lengths ka farak bhi leak na ho.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));

        if (!CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Admin")
            },
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
