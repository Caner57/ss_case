using ConfigReader.Admin.Api.Application;

namespace ConfigReader.Admin.Api.Authentication;

/// <summary>
/// Resolves the current actor from the authenticated request. Under the API-key scheme
/// (CFG-9.1) every valid caller shares the single "admin" identity, which becomes the audit
/// actor. If no identity is present the actor falls back to a non-empty sentinel so an audit
/// entry never records an empty "who".
/// </summary>
public sealed class HttpContextCurrentActor : ICurrentActor
{
    public const string UnknownActor = "unknown";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentActor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    public string Name
    {
        get
        {
            var name = _httpContextAccessor.HttpContext?.User.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? UnknownActor : name;
        }
    }
}
