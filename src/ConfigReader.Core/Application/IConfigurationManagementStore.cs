using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application;

/// <summary>
/// Storage-agnostic port for the management (Admin API) read surface. Unlike
/// <see cref="IConfigurationStore"/> — the library-facing read port that is deliberately
/// restricted to active records of a single application — this port intentionally exposes
/// every record across all applications, including inactive ones. That broader reach is a
/// conscious management privilege: the Admin UI must list and edit the entire catalogue,
/// whereas a consuming service may only ever see its own active configuration.
/// </summary>
public interface IConfigurationManagementStore
{
    /// <summary>Returns every configuration record across all applications (management view).</summary>
    Task<IReadOnlyList<ConfigurationRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single record by its identifier, or <c>null</c> if none exists.</summary>
    Task<ConfigurationRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
