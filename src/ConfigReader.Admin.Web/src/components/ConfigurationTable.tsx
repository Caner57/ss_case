import type { Configuration } from '../api/types';

interface ConfigurationTableProps {
  configurations: Configuration[];
  onEdit: (configuration: Configuration) => void;
}

/**
 * Renders the six configuration columns (Id, Name, Type, Value, IsActive, ApplicationName) in a
 * table, plus a per-row edit action. Filtering happens in the parent before rows reach this
 * component, so it stays a pure view.
 */
export function ConfigurationTable({ configurations, onEdit }: ConfigurationTableProps) {
  if (configurations.length === 0) {
    return <p className="empty-state">Eşleşen yapılandırma yok.</p>;
  }

  return (
    <table className="config-table">
      <thead>
        <tr>
          <th>Id</th>
          <th>Ad</th>
          <th>Tür</th>
          <th>Değer</th>
          <th>Aktif</th>
          <th>Uygulama</th>
          <th aria-label="İşlemler" />
        </tr>
      </thead>
      <tbody>
        {configurations.map((configuration) => (
          <tr key={configuration.id}>
            <td className="mono">{configuration.id}</td>
            <td>{configuration.name}</td>
            <td>{configuration.type}</td>
            <td className="mono">{configuration.value}</td>
            <td>{configuration.isActive ? 'Evet' : 'Hayır'}</td>
            <td>{configuration.applicationName}</td>
            <td>
              <button type="button" className="edit-button" onClick={() => onEdit(configuration)}>
                <svg
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
                >
                  <path d="M12 20h9" />
                  <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z" />
                </svg>
                <span>Düzenle</span>
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
