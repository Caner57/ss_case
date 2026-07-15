namespace ConfigReader.Admin.Api.Middleware;

/// <summary>
/// Adds baseline browser hardening headers to every response: <c>X-Content-Type-Options: nosniff</c>
/// (stops MIME sniffing) and <c>X-Frame-Options: DENY</c> (stops clickjacking via framing).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";

        return _next(context);
    }
}
