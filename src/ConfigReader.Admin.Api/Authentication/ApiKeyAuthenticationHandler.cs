using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ConfigReader.Admin.Api.Authentication;

/// <summary>
/// Minimal API-key authentication: a request is authenticated when it carries a valid
/// <c>X-Api-Key</c> header matching the configured admin key. Chosen over JWT because the case
/// needs no user/role model — a single trusted management surface — so a shared secret is the
/// smallest real auth layer. It sits behind ASP.NET's authentication abstraction, so swapping in
/// JWT later is a composition-root change, not a controller change. The secret comes from
/// configuration/environment (CFG-9.4), never from code, and the API fails closed if it is unset.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ConfigurationKey = "AdminApi:ApiKey";

    private readonly string? _configuredKey;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuredKey = configuration[ConfigurationKey];
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey) || StringValues.IsNullOrEmpty(providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrWhiteSpace(_configuredKey))
        {
            Logger.LogError("Admin API key is not configured; rejecting the request (fail closed).");
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        if (!IsMatch(providedKey.ToString(), _configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool IsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // Constant-time comparison avoids leaking the key length/content via response timing.
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
