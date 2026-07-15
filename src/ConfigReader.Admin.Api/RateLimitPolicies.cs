namespace ConfigReader.Admin.Api;

/// <summary>Named rate-limiting policies applied to the API's write surface.</summary>
public static class RateLimitPolicies
{
    /// <summary>Throttles create/update calls to blunt automated abuse and resource exhaustion.</summary>
    public const string Write = "write";
}
