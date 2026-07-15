namespace ConfigReader.Admin.Api.Application;

/// <summary>
/// Supplies the identity of the caller performing a management action, so the use-case layer
/// can stamp audit entries without depending on ASP.NET's <c>HttpContext</c> directly. The
/// concrete implementation reads the authenticated <c>ClaimsPrincipal</c> established by CFG-9.1.
/// </summary>
public interface ICurrentActor
{
    string Name { get; }
}
